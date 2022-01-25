using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Telegram.Bot;
using Telegram.Bot.Types;

namespace DebtSlaveryBot.Bot.Scenario
{
    class GetDebtsScenario : TgBotUserEventScenario
    {
        public GetDebtsScenario(ILogger<IBotService> logger, ITelegramBotClient botClient)
            : base(logger, botClient)
        {
            ScheduleNext(OnStart);
        }

        private Model.DebtEvent ActiveEvent;

        private async Task ReplyUserBorrowers(Model.IDebtManager manager, Model.User creditor, long chatId, bool writeName)
        {
            var borrowers = manager.GetBorrowers(creditor.Name);
            if (!borrowers.Any())
            {
                await BotClient.SendTextMessageAsync(chatId, "Должников нет");
                return;
            }
            var response = new StringBuilder();
            var num = 1;
            if (writeName)
            {
                response.Append($"Заёмщик: *{creditor.Name}*\n");
            }
            foreach (var entry in borrowers)
            {
                response.Append($"{num++}. Должник: *{entry.Key.TgName}*, сумма: *{entry.Value}*\n");
            }
            await BotClient.SendTextMessageAsync(chatId, response.ToString(), parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
        }

        private const string RequestUserNameString = "Введите заёмщика (@имя пользователя в Telegram или поделитесь контактом)";

        public async Task<bool> OnStart(Message message)
        {
            var manager = Global.Services.GetService<Model.IDebtManager>();

            // temp default event
            // todo remove
            ActiveEvent = manager.GetEvent(Helpers.Defaults.DefaultEvent);
            
            var currentUser = manager.GetUser(message.From.Id);
            
            if (currentUser == null)
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, "Пользователь не найден, сделайте /start :)");
                return true;
            }
            if (!currentUser.TgDetails.ImpersonalMode)
            {
                await ReplyUserBorrowers(manager, currentUser, message.Chat.Id, false);
                return true;
            }
            await BotClient.SendTextMessageAsync(message.Chat.Id, "*impersonal mode*", Telegram.Bot.Types.Enums.ParseMode.Markdown);
            ScheduleNext(OnCreditorNameReceived);
            if (ActiveEvent != null)
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id,
                    BuildUserListFromEvent("Введите заёмщика", ActiveEvent, null,
                    "Или @имя пользователя в Telegram или поделитесь контактом"));
            }
            else
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, RequestUserNameString);
            }
            return true;
        }

        public async Task<bool> OnCreditorNameReceived(Message message)
        {
            try
            {
                var creditor = GetUserFromMessage(message, ActiveEvent, null);
                if (creditor == null)
                {
                    throw new ScenarioLogicalException($"Такой пользователь не найден, попробуйте еще раз\n{RequestUserNameString}");
                }
                var manager = Global.Services.GetService<Model.IDebtManager>();
                await ReplyUserBorrowers(manager, creditor, message.Chat.Id, true);
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
