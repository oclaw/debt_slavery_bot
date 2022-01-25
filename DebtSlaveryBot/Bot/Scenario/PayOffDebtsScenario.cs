using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;


namespace DebtSlaveryBot.Bot.Scenario
{
    class PayOffDebtsScenario : TgBotUserEventScenario
    {
        public PayOffDebtsScenario(ILogger<IBotService> logger, ITelegramBotClient botClient)
            : base(logger, botClient)
        {
            ScheduleNext(OnStart);
        }

        private class TaskContext
        {
            public Model.User Creditor;
            public Dictionary<Model.User, decimal> TotalDebtInfo;
            public List<Model.User> Borrowers = new List<Model.User>();
            public Model.User SelectedBorrower;
            public Model.DebtEvent ActiveEvent;
        }

        private TaskContext Context = new TaskContext();

        private async Task SendBorrowerList(long chatId)
        {
            var manager = Global.Services.GetService<Model.IDebtManager>();
            var borrowers = manager.GetBorrowers(Context.Creditor.Name);
            if (!borrowers.Any())
            {
                await BotClient.SendTextMessageAsync(chatId, "Должников нет");
                return;
            }

            var response = new StringBuilder();
            var num = 1;
            response.Append("Кого раскрепощаем?)\n");
            foreach (var entry in borrowers)
            {
                Context.Borrowers.Add(entry.Key); // getting ordered list
                response.Append($"{num++}. *{entry.Key.TgName}* (сумма: *{entry.Value}*)\n");
            }
            response.Append("Введите @username или выберите номер в списке");

            Context.TotalDebtInfo = borrowers;

            ScheduleNext(OnBorrowerSelected);

            await BotClient.SendTextMessageAsync(chatId, response.ToString(), parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
        }

        private const string RequestCreditorString = "Введите заёмщика (@имя пользователя в Telegram или поделитесь контактом)";

        public async Task<bool> OnStart(Message message)
        {
            try
            {
                var manager = Global.Services.GetService<Model.IDebtManager>();

                // temp default event
                // todo remove
                Context.ActiveEvent = manager.GetEvent(Helpers.Defaults.DefaultEvent);

                var me = manager.GetUser(message.From.Id);
                if (me?.TgDetails == null)
                {
                    await BotClient.SendTextMessageAsync(message.Chat.Id, "Вы не начали работу с ботом. /start должен помочь :)");
                    return true;
                }
                if (!me.TgDetails.ImpersonalMode)
                {
                    Context.Creditor = me;
                    await SendBorrowerList(message.Chat.Id);
                    return true;
                }
                else
                {
                    await BotClient.SendTextMessageAsync(message.Chat.Id, "*impersonal mode*", Telegram.Bot.Types.Enums.ParseMode.Markdown);
                    ScheduleNext(OnCreditorNameRecevied);
                    if (Context.ActiveEvent != null)
                    {
                        await BotClient.SendTextMessageAsync(message.Chat.Id,
                            BuildUserListFromEvent("Введите заёмщика", Context.ActiveEvent, null, "Или @имя пользователя в Telegram или поделитесь контактом"));
                    }
                    else
                    {
                        await BotClient.SendTextMessageAsync(message.Chat.Id, RequestCreditorString);
                    }
                    return true;
                }
            }
            catch (ScenarioLogicalException exc)
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, exc.Message);
            }
            catch (Model.DataInconsistentException dataExc)
            {
                Logger.LogError($"Error on getting info from db: {dataExc.Message}");
            }
            return true;
        }

        private async Task<bool> OnCreditorNameRecevied(Message message)
        {
            try
            {
                var user = GetUserFromMessage(message, Context.ActiveEvent, null);
                if (user == null)
                {
                    throw new ScenarioLogicalException($"Такой пользователь не найден :(\n{RequestCreditorString}");
                }
                Context.Creditor = user;
                await SendBorrowerList(message.Chat.Id);
                return true;
            }
            catch (ScenarioLogicalException exc)
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, exc.Message);
                return false;
            }
        }

        private Model.User GetUser(string request, string errorText)
        {
            if (request.StartsWith('@'))
            {
                try
                {
                    return Context.Borrowers.First(i => i.TgName == request);
                }
                catch (InvalidOperationException)
                {
                    throw new ScenarioLogicalException(errorText);
                }
            }
            else if (int.TryParse(request, out int listIdx))
            {
                listIdx--;
                if (listIdx >= 0 && listIdx < Context.Borrowers.Count)
                {
                    return Context.Borrowers[listIdx];
                }
            }
            throw new ScenarioLogicalException(errorText);
        }

        const string WriteOffAllText = "Списать все";

        public async Task<bool> OnBorrowerSelected(Message message)
        {
            try
            {
                string errorText = "Введите @username или выберите номер в списке";
                if (message.Type != Telegram.Bot.Types.Enums.MessageType.Text)
                {
                    throw new ScenarioLogicalException(errorText);
                }
                var user = GetUser(message.Text, errorText);
                var manager = Global.Services.GetService<Model.IDebtManager>();

                Context.SelectedBorrower = user;

                ScheduleNext(OnWriteOffSumArrived);

                await BotClient.SendTextMessageAsync(message.Chat.Id, "Сколько списываем?",
                    replyMarkup: new ReplyKeyboardMarkup(new KeyboardButton(WriteOffAllText)  
                ) { ResizeKeyboard = true});

                return true;
            }
            catch (ScenarioLogicalException exc)
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, exc.Message);
                return false;
            }
        }

        public async Task WriteOffAll(long curChatId)
        {
            var manager = Global.Services.GetService<Model.IDebtManager>();

            var borrower = Context.SelectedBorrower;

            manager.WriteOffDebts(borrower.Name, Context.Creditor.Name);
            manager.WriteOffDebts(Context.Creditor.Name, borrower.Name);

            await BotClient.SendTextMessageAsync(curChatId, $"{borrower.TgName} свободен!", replyMarkup: new ReplyKeyboardRemove());
            var (notify, chatId) = BotService.GetPrimaryChatId(borrower.TgDetails.Id);
            if (notify)
            {
                await BotClient.SendTextMessageAsync(chatId, $"Списаны все долги к {Context.Creditor.TgName} :)");
            }
        }

        public async Task WriteOffPart(long curChatId, decimal sum)
        {
            var manager = Global.Services.GetService<Model.IDebtManager>();

            var borrower = Context.SelectedBorrower;
            var debts = manager.GetActiveDebts(borrower.Name, Context.Creditor.Name);

            if (!debts.Any())
            {
                throw new InvalidProgramException("Debts and TotalDebts are not synced, service failure");
            }

            var savedSum = sum;
            while (sum > 0)
            {
                var prefferedToWriteOff = debts.Where(i => !i.Paid).OrderBy(i => Math.Abs(sum - i.LeftSum)).First();
                var debtLeftSum = prefferedToWriteOff.LeftSum;
                if (sum >= debtLeftSum)
                {
                    manager.WriteOffDebt(prefferedToWriteOff.Id);
                }
                else
                {
                    manager.WriteOffDebtPartially(prefferedToWriteOff.Id, sum);
                }
                sum -= debtLeftSum;
            }

            await BotClient.SendTextMessageAsync(curChatId, $"Готово :)", replyMarkup: new ReplyKeyboardRemove());
            var (notify, chatId) = BotService.GetPrimaryChatId(borrower.TgDetails.Id);
            if (notify)
            {
                await BotClient.SendTextMessageAsync(chatId, $"Списан долг (сумма {savedSum}) к {Context.Creditor.TgName} :)");
            }
        }

        public async Task<bool> OnWriteOffSumArrived(Message message)
        {
            try
            {
                var totalSum = Context.TotalDebtInfo[Context.SelectedBorrower];
                string errorText = $"Введите сумму списания (не более {totalSum}) или нажмите '{WriteOffAllText}'";
                if (message.Type != Telegram.Bot.Types.Enums.MessageType.Text)
                {
                    throw new ScenarioLogicalException(errorText);
                }
                
                string text = message.Text;
                if (text == WriteOffAllText)
                {
                    await WriteOffAll(message.Chat.Id);
                    return true;
                }

                if (text.Contains(','))
                {
                    throw new ScenarioLogicalException(errorText);
                }
                if (!decimal.TryParse(text, out decimal sum))
                {
                    throw new ScenarioLogicalException(errorText);
                }
                if (sum <= 0 || sum > totalSum)
                {
                    throw new ScenarioLogicalException(errorText);
                }
                sum = Math.Round(sum, 2);

                if (sum == totalSum)
                {
                    await WriteOffAll(message.Chat.Id);
                    return true;
                }
                
                await WriteOffPart(message.Chat.Id, sum);

                var manager = Global.Services.GetService<Model.IDebtManager>();
                var user = manager.GetUser(message.From.Id);
                if (Context.Creditor != user)
                {
                    var (notify, chatId) = BotService.GetPrimaryChatId(Context.Creditor.TgDetails.Id);
                    if (notify)
                    {
                        await BotClient.SendTextMessageAsync(chatId, 
                            $"Пользователь {user.TgName} списал долг от {Context.SelectedBorrower.TgName} (сумма {sum})"); 
                    }
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
