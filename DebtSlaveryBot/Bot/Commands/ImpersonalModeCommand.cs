using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Telegram.Bot;
using Telegram.Bot.Types;

using Microsoft.Extensions.Logging;

namespace DebtSlaveryBot.Bot.Commands
{
    internal class ImpersonalModeCommand : ExtendedBotCommand
    {
        public ImpersonalModeCommand(ILogger<IBotService> logger, IServiceProvider serviceProvider, string botName) :
            base(logger, serviceProvider, botName)
        {
            Command = "/set_impersonal_mode";
            Description = "see/edit debts of other accounts";
        }

        public override async Task Execute(Message message)
        {
            Bot.RunScenario(new ChatEntry(message),
                       new Scenario.ImpersonalModeScenario(Logger, ServiceProvider));
        }
    }
}
