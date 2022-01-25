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
        public ImpersonalModeCommand(ILogger<IBotService> logger, string botName) :
            base(logger, botName)
        {
            Command = "/set_impersonal_mode";
            Description = "see/edit debts of other accounts";
        }

        public override async Task Execute(ITelegramBotClient client, IBotService botService, Message message)
        {
            botService.RunScenario(new ChatEntry(message),
                       new Scenario.ImpersonalModeScenario(Logger, client));
        }
    }
}
