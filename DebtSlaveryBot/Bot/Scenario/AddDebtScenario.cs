using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

using DebtSlaveryBot.Bot.Helpers;


namespace DebtSlaveryBot.Bot.Scenario
{
    class AddDebtScenario : TgBotUserEventScenario
    {
        public AddDebtScenario(ILogger<IBotService> _logger, IServiceProvider serviceProvider)
            : base(_logger, serviceProvider)
        {
            ScheduleNext(OnStart);
        }

        private class BorrowerInfo
        {
            public Model.User User;
            public decimal Sum;
        }

        private class TaskContext
        {
            public List<BorrowerInfo> Borrowers = new List<BorrowerInfo>();
            public Model.User Creditor;
            public string Description;
            public Model.DebtEvent ActiveEvent;
        }

        private TaskContext Context = new TaskContext();

        private const string RequestCreditorNameString = "Введите пользователя-заёмщика (@имя пользователя в Telegram или поделитесь контактом)";
        private const string RequestUserNameString = "Введите имя должника (@имя пользователя в Telegram или поделитесь контактом)";
        private const string RequestDescriptionString = "Введите описание долга";

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
            ScheduleNext(OnBorrowerNameReceived);
            await RequestBorrowerName(message.Chat.Id, false);
            return true;
        }

        private async Task RequestBorrowerName(long chatId, bool creditor, IReplyMarkup markup = null)
        {
            if (Context.ActiveEvent != null)
            {
                string listPrefix = creditor ?
                    "Выберите заёмщика из списка" :
                    "Выберите должника из списка";

                await BotClient.SendTextMessageAsync(chatId,
                    BuildUserListFromEvent(listPrefix, Context.ActiveEvent, Context.Creditor, "Или введите @имя или контакт"), replyMarkup: markup);
            }
            else
            {
                await BotClient.SendTextMessageAsync(chatId,
                    creditor ? RequestCreditorNameString : RequestUserNameString, replyMarkup: markup);
            }
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
                ScheduleNext(OnBorrowerNameReceived);
                await RequestBorrowerName(message.Chat.Id, false);
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
                var entry = Context.Borrowers.Last();
                entry.Sum = CalcSum(message.Text, errorText);

                ScheduleNext(OnAddMoreBorrowersQuestionAnswered);
                await BotClient.SendTextMessageAsync(chatId: message.Chat.Id, text: "Добавить еще одного должника?", replyMarkup: MarkupHelpers.YesNoButtonTemplate);

                return true;
            }
            catch (ScenarioLogicalException exc)
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, exc.Message);
                return false;
            }
        }

        private async Task<bool> OnBorrowerNameReceived(Message message)
        {
            try
            {
                var user = GetUserFromMessage(message, Context.ActiveEvent, Context.Creditor);
                if (user == null)
                {
                    throw new ScenarioLogicalException($"Такой пользователь не найден, пусть зарегистрируется (или что-то не так введено)\n{RequestUserNameString}");
                }
                Context.Borrowers.Add(new BorrowerInfo { User = user });
                ScheduleNext(OnDebtSumReceived);
                await BotClient.SendTextMessageAsync(message.Chat.Id, "Введите сумму долга");
                return true;
            }
            catch (ScenarioLogicalException exc)
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, exc.Message);
                return false;
            }
        }

        private async Task<bool> OnAddMoreBorrowersQuestionAnswered(Message message)
        {
            try
            {
                string errorText = $"{MarkupHelpers.YesText}/{MarkupHelpers.NoText}?";
                if (message.Type != MessageType.Text)
                {
                    throw new ScenarioLogicalException(errorText);
                }
                if (message.Text == MarkupHelpers.YesText)
                {
                    await RequestBorrowerName(message.Chat.Id, false, new ReplyKeyboardRemove());
                    ScheduleNext(OnBorrowerNameReceived);
                    return true;
                }
                else if (message.Text == MarkupHelpers.NoText)
                {
                    await BotClient.SendTextMessageAsync(chatId: message.Chat.Id, text: RequestDescriptionString, replyMarkup: new ReplyKeyboardRemove());
                    ScheduleNext(OnDescriptionReceived);
                    return true;
                }
                else
                {
                    throw new ScenarioLogicalException(errorText);
                }
            }
            catch (ScenarioLogicalException exc)
            {
                await BotClient.SendTextMessageAsync(chatId: message.Chat.Id, text: exc.Message, replyMarkup: MarkupHelpers.YesNoButtonTemplate);
                return false;
            }
        }

        private async void AddDebts(long chatId)
        {
            try
            {
                var manager = DebtManager;
                
                foreach (var borrower in Context.Borrowers)
                {
                    manager.AddDebt(borrower.User.Name, Context.Creditor.Name, borrower.Sum, Context.Description, Context.ActiveEvent?.Name);
                }
            }
            catch (Exception exc)
            {
                await BotClient.SendTextMessageAsync(chatId, $"Что-то пошло не так :(\n Возможно вы не состоите в текущем событии (потом починим)");
                Logger.LogError($"AddDebtScenario: error occured on saving request data to DB: {exc.Message}");
            }
        }

        private async Task NotifyBorrower(BorrowerInfo borrower, StringBuilder creditorNotification)
        {
            var manager = DebtManager;

            var (notify, chatId) = BotService.GetPrimaryChatId(borrower.User.TgDetails.Id);
            if (notify)
            {
                var totalSum = manager.GetTotalDebtSum(borrower.User.Name, Context.Creditor.Name);
                var notification = $"Добавлен долг к {Context.Creditor.TgName} на сумму {borrower.Sum}: {Context.Description}\n";
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
            if (creditorNotification != null)
            {
                creditorNotification.AppendLine($"Долг от {borrower.User.TgName} на сумму {borrower.Sum}");
            }
        }

        private async Task<bool> OnDescriptionReceived(Message message)
        {
            try
            {
                var manager = DebtManager;

                string errorText = RequestDescriptionString;
                if (message.Type != MessageType.Text)
                {
                    throw new ScenarioLogicalException(errorText);
                }
                Context.Description = message.Text;
                AddDebts(message.Chat.Id);
                await BotClient.SendTextMessageAsync(message.Chat.Id, "Готово :)");
                var service = BotService;

                StringBuilder creditorNotification = null;
                bool notifyCreditor = false;
                long creditorChatId = 0;
                var user = manager.GetUser(message.From.Id);
                if (user != Context.Creditor)
                {
                    Logger.LogDebug($"Impersonal mode: (user {user.Name}), trying to send notification to creditor ({Context.Creditor.Name})");
                    (notifyCreditor, creditorChatId) = service.GetPrimaryChatId(Context.Creditor.TgDetails.Id);
                    if (notifyCreditor)
                    {
                        creditorNotification = new StringBuilder($"Пользователь {user.TgName} добавил к вам должников: \n");
                    }
                }

                foreach (var borrower in Context.Borrowers)
                    await NotifyBorrower(borrower, creditorNotification);

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
    }
}
