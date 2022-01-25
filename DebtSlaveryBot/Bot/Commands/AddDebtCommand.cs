using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Telegram.Bot.Types;

using Microsoft.Extensions.Logging;

namespace DebtSlaveryBot.Bot.Commands
{
    internal class AddDebtCommand : ExtendedBotCommand
    {
        public AddDebtCommand(ILogger<IBotService> logger, string botName) :
            base(logger, botName)
        {
            Command = "/add_debt";
            Description = "add debt from one or more users to you";
        }

        public override async Task Execute(Telegram.Bot.ITelegramBotClient client, IBotService botService, Message message)
        {
            botService.RunScenario(new ChatEntry(message), new Scenario.AddDebtScenario(Logger, client));
        }
    }
}
