using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;


using DebtSlaveryBot.Bot.Helpers;

namespace DebtSlaveryBot.Bot.Scenario
{
    class ShareDebtScenario : TgBotUserEventScenario
    {
        public ShareDebtScenario(ILogger<IBotService> _logger, IServiceProvider serviceProvider)
            : base(_logger, serviceProvider)
        {
            ScheduleNext(OnStart);
        }

        private class TaskContext
        {
            public decimal Sum;

            // Do not call before Borrowers and ActiveEvent are initialized!
            public decimal SumPerUser => Math.Round(Borrowers != null ?
                                            Sum / (Borrowers.Count + 1) : Sum / (ActiveEvent.Users.Count), 2);

            public Model.User Creditor;
            public List<Model.User> Borrowers = new List<Model.User>();
            public string Description;
            public Model.DebtEvent ActiveEvent;
        }

        private readonly TaskContext Context = new();


        private const string ShareToAllText = "Разделить на всех";
        private static ReplyMarkupBase ShareAllMarkup => new ReplyKeyboardMarkup(
            new KeyboardButton(ShareToAllText)) { ResizeKeyboard = true};

        private async Task<bool> OnStart(Message message)
        {
            var manager = DebtManager;

            // temp default event
            // todo remove
            Context.ActiveEvent = manager.GetEvent(Defaults.DefaultEvent);

            var currentUser = manager.GetUser(message.From.Id);
            if (currentUser?.TgDetails == null)
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, "Не вижу вас в базе, /start должен помочь :)");
                return true;
            }

            if (currentUser.TgDetails.ImpersonalMode)
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, "*impersonal mode*", ParseMode.Markdown);
                ScheduleNext(OnCreditorNameReceived);
                await RequestBorrowerName(message.Chat.Id, true);
                return true;
            }

            Context.Creditor = currentUser;
            ScheduleNext(OnBorrowerNamesReceived);

            await RequestBorrowerName(message.Chat.Id, false, ShareAllMarkup);

            return true;
        }

        private async Task<bool> OnCreditorNameReceived(Message message)
        {
            try
            {
                Context.Creditor = GetUserFromMessage(message, Context.ActiveEvent, null);
                if (Context.Creditor == null)
                {
                    throw new ScenarioLogicalException($"Такой пользователь не найден, пусть зарегистрируется (или что-то не так введено)\n{RequestCreditorNameString}");
                }
                ScheduleNext(OnBorrowerNamesReceived);
                await RequestBorrowerName(message.Chat.Id, false, ShareAllMarkup);
                return true;
            }
            catch (ScenarioLogicalException exc)
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, exc.Message);
                return false;
            }
        }

        private async Task<bool> OnBorrowerNamesReceived(Message message)
        {
            try
            {
                if (message.Text == ShareToAllText)
                {
                    if (Context.ActiveEvent == null)
                        throw new ScenarioLogicalException(
                            $"Не выбрано активное событие, разделить на всех не получится\n{RequestUserNameString}");

                    Context.Borrowers = null;
                }
                else
                {
                    if (GetUserListFromMessage(
                            message, Context.ActiveEvent, Context.Creditor) is var borrowers
                                && borrowers == null)
                    {
                        throw new ScenarioLogicalException(RequestUserNameString);
                    }
                    Context.Borrowers = borrowers;
                }
                ScheduleNext(OnDebtSumReceived);
                await BotClient.SendTextMessageAsync(
                    message.Chat.Id, "Введите сумму долга", replyMarkup: new ReplyKeyboardRemove());
                return true;
            }
            catch (ScenarioLogicalException exc)
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, exc.Message);
                return false;
            }
        }
        
        private async Task<bool> OnDebtSumReceived(Message message)
        {
            try
            {
                string errorText = "Введите сумму долга (число или простое выражение)";
                if (message.Type != MessageType.Text)
                {
                    throw new ScenarioLogicalException(errorText);
                }

                Context.Sum = CalcSum(message.Text, errorText);
                ScheduleNext(OnDescriptionReceived);
                await BotClient.SendTextMessageAsync(message.Chat.Id, "Введите описание долга");
                return true;
            }
            catch (ScenarioLogicalException exc)
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, exc.Message);
                return false;
            }
        }

        private async Task<bool> OnDescriptionReceived(Message message)
        {
            try
            {
                string errorText = RequestDescriptionString;
                if (message.Type != MessageType.Text)
                {
                    throw new ScenarioLogicalException(errorText);
                }
                Context.Description = message.Text;

                AddDebts();
                await BotClient.SendTextMessageAsync(message.Chat.Id, "Готово :)");

                var manager = DebtManager;

                StringBuilder creditorNotification = null;
                bool notifyCreditor = false;
                long creditorChatId = 0;

                var user = manager.GetUser(message.From.Id);
                if (user != Context.Creditor)
                {
                    Logger.LogDebug($"Impersonal mode: (user {user.Name}), trying to send notification to creditor ({Context.Creditor.Name})");
                    (notifyCreditor, creditorChatId) = BotService.GetPrimaryChatId(Context.Creditor.TgDetails.Id);
                    if (notifyCreditor)
                    {
                        creditorNotification = new StringBuilder($"Пользователь ${user.TgName} разделил долг ${Context.Sum} между: \n");
                    }
                }

                await NotifyBorrowers(creditorNotification);

                if (notifyCreditor)
                {
                    await BotClient.SendTextMessageAsync(creditorChatId, creditorNotification.ToString());
                }

                return true;
            }
            catch (ScenarioLogicalException exc)
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, exc.Message);
                return false;
            }
        }


        async Task NotifyBorrowers(StringBuilder creditorNotification)
        {
            var manager = DebtManager;

            IEnumerable<Model.User> borrowers = Context.Borrowers;
            if (borrowers == null)
            {
                borrowers = Context.ActiveEvent.Users.Where(u => u != Context.Creditor);
            }

            foreach (var borrower in borrowers)
            {
                await Notify(borrower);
                creditorNotification?.AppendLine($"${borrower.TgName}");
            }
        }

        async Task Notify(Model.User borrower)
        {
            var manager = DebtManager;

            var (notify, chatId) = BotService.GetPrimaryChatId(borrower.TgDetails.Id);
            if (!notify)
            {
                return;
            }

            var totalSum = manager.GetTotalDebtSum(borrower.Name, Context.Creditor.Name);
            var notification = $"Добавлен долг к {Context.Creditor.TgName} на сумму {Context.SumPerUser}: {Context.Description}\n";
            if (totalSum > 0)
            {
                notification += $"Общая сумма долга: {totalSum}";
            }
            else
            {
                notification += $"Cчет был в твою пользу, долга нет :)";
            }
            await BotClient.SendTextMessageAsync(chatId, notification);
        }

        private void AddDebts()
        {
            var manager = DebtManager;

            if (Context.Borrowers == null)
            {
                manager.ShareSum(Context.Sum, Context.Creditor.Name, Context.ActiveEvent.Name, Context.Description);
                return;
            }
            foreach (var borrower in Context.Borrowers)
            {
                manager.AddDebt(borrower.Name, Context.Creditor.Name, Context.SumPerUser, Context.Description, Context.ActiveEvent?.Name);
            }
        }


        private const string RequestCreditorNameString = "Введите пользователя-заёмщика (@имя пользователя в Telegram или поделитесь контактом)";
        private const string RequestUserNameString = "Введите номера пользователей-должников (через запятую или пробел)";
        private const string RequestDescriptionString = "Введите описание долга";

        private async Task RequestBorrowerName(long chatId, bool creditor, IReplyMarkup markup = null)
        {
            if (Context.ActiveEvent != null)
            {
                string listPrefix = creditor ?
                    "Выберите заёмщика из списка" :
                    RequestUserNameString;

                string listPostfix = creditor ?
                    "Или введите @имя или контакт" :
                    null;

                await BotClient.SendTextMessageAsync(chatId,
                    BuildUserListFromEvent(listPrefix, Context.ActiveEvent, Context.Creditor, listPostfix), replyMarkup: markup);
            }
            else
            {
                await BotClient.SendTextMessageAsync(chatId,
                    creditor ? RequestCreditorNameString : RequestUserNameString, replyMarkup: markup);
            }
        }
    }
}
