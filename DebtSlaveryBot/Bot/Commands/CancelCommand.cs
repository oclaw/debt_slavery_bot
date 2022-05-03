using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using DebtSlaveryBot.Bot.Helpers.Attributes;

namespace DebtSlaveryBot.Bot.Commands
{
    [BotCommand]
    class CancelCommand : ExtendedBotCommand
    {
        public CancelCommand(ILogger<IBotService> _logger, IServiceProvider serviceProvider, string botName)
            : base(_logger, serviceProvider, botName)
        {
            Command = "/cancel";
            Description = "Cancel current active scenario";
        }

        public async override Task Execute(Message messageData)
        {
            await TgClient.SendTextMessageAsync(messageData.Chat.Id, "Отмена", replyMarkup: new ReplyKeyboardRemove());
            Bot.ResetScenario(new ChatEntry(messageData)); 
        }
    }
}
