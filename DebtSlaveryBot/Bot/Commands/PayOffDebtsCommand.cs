using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Telegram.Bot;
using Telegram.Bot.Types;

using Microsoft.Extensions.Logging;

namespace DebtSlaveryBot.Bot.Commands
{
    internal class PayOffDebtsCommand : ExtendedBotCommand
    {
        public PayOffDebtsCommand(ILogger<IBotService> logger, IServiceProvider serviceProvider, string botName) :
            base(logger, serviceProvider, botName)
        {
            Command = "/pay_off_debts";
            Description = "pay off some debts from single user to you";
        }

        public override async Task Execute(Message message)
        {
            Bot.RunScenario(new ChatEntry(message),
                       new Scenario.PayOffDebtsScenario(Logger, ServiceProvider));
        }
    }
}
