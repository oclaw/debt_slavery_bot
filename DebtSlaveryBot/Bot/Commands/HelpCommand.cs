using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Telegram.Bot;
using Telegram.Bot.Types;

using DebtSlaveryBot.Bot.Helpers.Attributes;

namespace DebtSlaveryBot.Bot.Commands
{
    [BotCommand]
    class HelpCommand : ExtendedBotCommand
    {
        public HelpCommand(ILogger<IBotService> _logger, IServiceProvider serviceProvider, string botName)
            : base(_logger, serviceProvider, botName)
        {
            Command = "/help";
            Description = "Display help";
        }

        public async override Task Execute(Message messageData)
        {
            var response = "Debt Slavery Bot - бот для учета долгов\n" +
                "Общие команды: \n" +
                "/add_debt - начислить кому-нибудь долги\n" +
                "/share_debt - разделить долг с одним или более человеком\n" +
                "/get_all_debts - просмотреть всех своих должников\n" +
                "/get_my_debts - просмотреть всех, кому должен\n" +
                "/pay_off_debts - списать долги\n" +
                "/cancel - отменить текущий сценарий";
            await TgClient.SendTextMessageAsync(messageData.Chat.Id, response);
        }
    }
}