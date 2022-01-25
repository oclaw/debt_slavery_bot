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
        public GetAllDebtsCommand(ILogger<IBotService> logger, string botName) :
            base(logger, botName)
        {
            Command = "/get_all_debts";
            Description = "get information about all your borrowers";
        }

        public override async Task Execute(ITelegramBotClient client, IBotService botService, Message message)
        {
            botService.RunScenario(new ChatEntry(message),
                       new Scenario.GetDebtsScenario(Logger, client));
        }
    }
}