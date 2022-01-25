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
        public GetMyDebtsCommand(ILogger<IBotService> logger, string botName) :
            base(logger, botName)
        {
            Command = "/get_my_debts";
            Description = "get information about all your creditors";
        }

        public override async Task Execute(ITelegramBotClient client, IBotService botService, Message message)
        {
            botService.RunScenario(new ChatEntry(message),
                       new Scenario.GetMyDebtsScenario(Logger, client));
        }
    }
}