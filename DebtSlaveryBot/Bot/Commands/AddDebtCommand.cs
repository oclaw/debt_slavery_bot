using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Telegram.Bot.Types;

using Microsoft.Extensions.Logging;

using DebtSlaveryBot.Bot.Helpers.Attributes;

namespace DebtSlaveryBot.Bot.Commands
{
    [BotCommand]
    internal class AddDebtCommand : ExtendedBotCommand
    {
        public AddDebtCommand(ILogger<IBotService> logger, IServiceProvider serviceProvider, string botName) :
            base(logger, serviceProvider, botName)
        {
            Command = "/add_debt";
            Description = "add debt from one or more users to you";
        }

        public override async Task Execute(Message message)
        {
            Bot.RunScenario(new ChatEntry(message), new Scenario.AddDebtScenario(Logger, ServiceProvider));
        }
    }
}
