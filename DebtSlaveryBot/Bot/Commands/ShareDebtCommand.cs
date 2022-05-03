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
    internal class ShareDebtCommand : ExtendedBotCommand
    {
        public ShareDebtCommand(ILogger<IBotService> logger, IServiceProvider serviceProvider, string botName) :
            base(logger, serviceProvider, botName)
        {
            Command = "/share_debt";
            Description = "share debt between one or more users";
        }

        public override async Task Execute(Message message)
        {
            Bot.RunScenario(new ChatEntry(message), new Scenario.ShareDebtScenario(Logger, ServiceProvider));
        }
    }
}
