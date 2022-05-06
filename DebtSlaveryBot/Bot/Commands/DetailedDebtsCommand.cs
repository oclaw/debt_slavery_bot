using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Telegram.Bot.Types;

using DebtSlaveryBot.Bot.Helpers.Attributes;


namespace DebtSlaveryBot.Bot.Commands
{
    [BotCommand]
    internal class DetailedDebtsCommand : ExtendedBotCommand
    {
        public DetailedDebtsCommand(ILogger<IBotService> logger, IServiceProvider serviceProvider, string botName):
            base(logger, serviceProvider, botName)
        {
            Command = "/debt_details";
            Description = "get details about debts";
        }

        public override async Task Execute(Message message)
        {
            Bot.RunScenario(new ChatEntry(message),
                new Scenario.DetailedDebtsScenario(Logger, ServiceProvider));
        }

    }
}
