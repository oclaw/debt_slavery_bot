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
    class ImpersonalModeScenario : TelegramBotScenario
    {
        public ImpersonalModeScenario(ILogger<IBotService> logger, IServiceProvider serviceProvider)
            : base(logger, serviceProvider)
        {
            SetChain(OnStart, OnAnswerReceived);
        }

        public async Task<bool> OnStart(Message message)
        {
            await BotClient.SendTextMessageAsync(message.Chat.Id, "Включить impersonal режим?", 
                replyMarkup: Helpers.MarkupHelpers.YesNoButtonTemplate);
            return true;
        }

        public async Task<bool> OnAnswerReceived(Message message)
        {
            try
            {
                string errorText = "Да/Нет?";
                if (message.Type != Telegram.Bot.Types.Enums.MessageType.Text)
                {
                    throw new ScenarioLogicalException(errorText);
                }
                string text = message.Text;
                bool modeOn;
                if (text == Helpers.MarkupHelpers.YesText)
                {
                    modeOn = true;
                }
                else if (text == Helpers.MarkupHelpers.NoText)
                {
                    modeOn = false;
                }
                else
                {
                    throw new ScenarioLogicalException(errorText);
                }
                var manager = DebtManager;
                manager.SetImpersonalMode(message.From.Id, modeOn);
                await BotClient.SendTextMessageAsync(message.Chat.Id, $"impersonal режим {(modeOn ? "включен" : "выключен")}", replyMarkup: new ReplyKeyboardRemove());
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
