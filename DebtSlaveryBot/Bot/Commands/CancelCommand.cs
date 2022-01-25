using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace DebtSlaveryBot.Bot.Commands
{
    class CancelCommand : ExtendedBotCommand
    {
        public CancelCommand(ILogger<IBotService> _logger, string botName)
            : base(_logger, botName)
        {
            Command = "/cancel";
            Description = "Cancel current active scenario";
        }

        public async override Task Execute(ITelegramBotClient client, IBotService botService, Message messageData)
        {
            await client.SendTextMessageAsync(messageData.Chat.Id, "Отмена", replyMarkup: new ReplyKeyboardRemove());
            botService.ResetScenario(new ChatEntry(messageData)); 
        }
    }
}
