using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using DebtSlaveryBot.Model;

namespace DebtSlaveryBot.Bot.Commands
{
    internal class StartCommand : ExtendedBotCommand
    {
        public StartCommand(ILogger<IBotService> logger, string botName) :
            base(logger, botName)
        {
            Command = "/start";
            Description = "initialize bot";
        }

        public override async Task Execute(Telegram.Bot.ITelegramBotClient client, IBotService botService, Message message)
        {
            var manager = Global.Services.GetService<IDebtManager>();
            var tgFrom = message.From;
            var uname = string.IsNullOrWhiteSpace(tgFrom.Username) ? tgFrom.Id.ToString() : tgFrom.Username;
            var user = manager.GetUser(uname);

            if (user == null)
            {
                Logger.LogInformation($"Registering new tg user {uname}");
                TgDetails details = new TgDetails(tgFrom);
                if (message.Chat.Type == Telegram.Bot.Types.Enums.ChatType.Private)
                {
                    details.PrivateChatId = message.Chat.Id;
                }
                user = manager.AddUser(uname, details);

                // temp default event
                // todo remove
                manager.LinkUserToEvent(uname, Helpers.Defaults.DefaultEvent);

                await client.SendTextMessageAsync(message.Chat.Id, $"Hello @{user.Name} :)");
            }
            else
            {
                Logger.LogInformation($"Found tg user {uname} in db!");
                await client.SendTextMessageAsync(chatId: message.Chat.Id, text: $"Welcome back @{user.Name} :)");
            }
        }
    }
}
