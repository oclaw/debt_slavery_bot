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
    class GetMyDebtsScenario : TelegramBotScenario
    {
        public GetMyDebtsScenario(ILogger<IBotService> logger, ITelegramBotClient botClient)
            : base(logger, botClient)
        {
            ScheduleNext(OnStart);
        }

        public async Task<bool> OnStart(Message message)
        {
            try
            {
                var manager = Global.Services.GetService<Model.IDebtManager>();
                var me = manager.GetUser(message.From.Id);
                if (me == null)
                {
                    throw new ScenarioLogicalException("Вы не начали работу с ботом. /start должен помочь :)");
                }
                var borrowers = manager.GetCreditors(me.Name);
                if (!borrowers.Any())
                {
                    await BotClient.SendTextMessageAsync(message.Chat.Id, "Долгов нет :)");
                    return true;
                }
                var response = new StringBuilder();
                var num = 1;
                foreach (var entry in borrowers)
                {
                    response.Append($"{num++}. Заёмщик: *{entry.Key.TgName}*, должен: *{entry.Value}*\n");
                }
                await BotClient.SendTextMessageAsync(message.Chat.Id, response.ToString(), parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return true;
            }
            catch (ScenarioLogicalException exc)
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, exc.Message);
            }
            catch (Model.DataInconsistentException dataExc)
            {
                Logger.LogError($"Error on getting info from db: {dataExc.Message}");
            }
            await BotClient.SendTextMessageAsync(message.Chat.Id, "Произошла ошибка :(");
            return true; // retry is not required for one-function scenario
        }
    }
}
