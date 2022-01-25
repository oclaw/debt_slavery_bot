using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Telegram.Bot;
using Telegram.Bot.Types;

namespace DebtSlaveryBot.Bot.Commands
{
    internal class GetAllDebtsCommand : ExtendedBotCommand
    {
        public GetAllDebtsCommand(ILogger<IBotService> logger, IServiceProvider serviceProvider, string botName) :
            base(logger, serviceProvider, botName)
        {
            Command = "/get_all_debts";
            Description = "get information about all your borrowers";
        }

        public override async Task Execute(Message message)
        {
            Bot.RunScenario(new ChatEntry(message),
                       new Scenario.GetDebtsScenario(Logger, ServiceProvider));
        }
    }
}