using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

using Microsoft.Extensions.Logging;

namespace DebtSlaveryBot.Bot.Commands
{
    internal class ExtendedBotCommand : BotCommand
    {
        protected readonly ILogger<IBotService> Logger;

        private string BotName;

        private string NamedBotCall => $"@{BotName}";

        protected ExtendedBotCommand(ILogger<IBotService> logger, string botName)
        {
            Logger = logger;
            BotName = botName;
        }

        public bool Matches(Message message)
        {
            if (message.Type != MessageType.Text)
            {
                return false;
            }
            string command = message.Text.TrimEnd();
            if (command.EndsWith(NamedBotCall))
            {
                command = command.Replace(NamedBotCall, "");
            }
            if (command == Command)
            {
                Logger.LogInformation($"recv command {command}");
                return true;
            }
            return false;
        }

        public virtual async Task Execute(ITelegramBotClient client, IBotService botService, Message messageData)
        {
            Logger.LogDebug($"Message passed to command '{Command}' handler!");
            await client.SendTextMessageAsync(chatId: messageData.Chat.Id,
                                              text: $"command '{Command}' not implemented yet :c");
        }
    }
}
