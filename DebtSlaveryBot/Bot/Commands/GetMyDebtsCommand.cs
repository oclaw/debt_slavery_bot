using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Telegram.Bot;
using Telegram.Bot.Types;

namespace DebtSlaveryBot.Bot.Commands
{
    internal class GetMyDebtsCommand : ExtendedBotCommand
    {
        public GetMyDebtsCommand(ILogger<IBotService> logger, IServiceProvider serviceProvider, string botName) :
            base(logger, serviceProvider, botName)
        {
            Command = "/get_my_debts";
            Description = "get information about all your creditors";
        }

        public override async Task Execute(Message message)
        {
            Bot.RunScenario(new ChatEntry(message),
                       new Scenario.GetMyDebtsScenario(Logger, ServiceProvider));
        }
    }
}