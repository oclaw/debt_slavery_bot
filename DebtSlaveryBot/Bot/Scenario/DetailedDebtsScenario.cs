using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

using DebtSlaveryBot.Bot.Helpers;

namespace DebtSlaveryBot.Bot.Scenario
{
    internal class DetailedDebtsScenario : TgBotUserEventScenario
    {
        public DetailedDebtsScenario(ILogger<IBotService> _logger, IServiceProvider serviceProvider)
            : base(_logger, serviceProvider)
        {
            ScheduleNext(OnStart);
        }

        class PossibleTarget
        {
            public bool IsCreditor { get; set; }
            public Model.User User { get; set; }
        }

        private class TaskContext
        {
            public Model.User Requester;              // who asks
            public List<PossibleTarget> PossibleTargets;  // DebtSum != 0
            public Model.DebtEvent ActiveEvent;
        }

        private TaskContext Context = new TaskContext();

        private async Task<bool> OnStart(Message message)
        {
            var manager = DebtManager;

            // temp default event
            // todo remove
            Context.ActiveEvent = manager.GetEvent(Defaults.DefaultEvent);

            var currentUser = manager.GetUser(message.From.Id);
            if (currentUser?.TgDetails == null)
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, "Не вижу вас в базе, /start должен помочь :)");
                return true;
            }
            if (currentUser.TgDetails.ImpersonalMode)
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, "*impersonal mode*", ParseMode.Markdown);
                ScheduleNext(OnRequesterReceived);
                await GetRequester(message.Chat.Id);
                return true;
            }
            Context.Requester = currentUser;
            RequestTarget(message.Chat.Id);
            return true;
        }


        private const string RequestCreditorNameString = "Введите пользователя-заёмщика (@имя пользователя в Telegram или поделитесь контактом)";
        private const string RequestUserNameString = "Введите имя должника (@имя пользователя в Telegram или поделитесь контактом)";

        private async Task GetRequester(long chatId)
        {
            if (Context.ActiveEvent != null)
            {
                string listPrefix = "Кто интересуется?";

                await BotClient.SendTextMessageAsync(chatId,
                    BuildUserListFromEvent(listPrefix, Context.ActiveEvent, Context.Requester, "Или введите @имя или контакт"));
            }
            else
            {
                await BotClient.SendTextMessageAsync(chatId, "Кто интересуется? (@имя или контакт)");
            }
        }

        private async void RequestTarget(long chatId)
        {
            var manager = DebtManager;
            var borrowers = manager.GetBorrowers(Context.Requester.Name);
            var creditors = manager.GetCreditors(Context.Requester.Name);

            Context.PossibleTargets = borrowers.Select(
                u => new PossibleTarget { User = u.Key, IsCreditor = false }).ToList();

            Context.PossibleTargets.AddRange(creditors.Select(u =>
                new PossibleTarget { User = u.Key, IsCreditor = true }));

            if (Context.PossibleTargets.Count == 0)
            {
                await BotClient.SendTextMessageAsync(chatId, "Долгов нет, деталей тоже :)");
                return;
            }

            var builder = new StringBuilder();
            int idx = 1;

            builder.AppendLine("По кому нужна информация?");
            foreach (var borrower in borrowers)
            {
                builder.Append($"{idx++}. {borrower.Key.TgName} (должен вам {borrower.Value})\n");
            }
            foreach (var creditor in creditors)
            {
                builder.Append($"{idx++}. {creditor.Key.TgName} (вы должны {creditor.Value})\n");
            }
            builder.AppendLine("Выберите из списка или введите @имя или контакт");

            await BotClient.SendTextMessageAsync(chatId, builder.ToString());
            ScheduleNext(OnTargetReceived);
        }

        private async Task<bool> OnRequesterReceived(Message message)
        {
            try
            {
                var manager = DebtManager;

                Context.Requester = GetUserFromMessage(message, Context.ActiveEvent, null);
                if (Context.Requester == null)
                {
                    throw new ScenarioLogicalException($"Такой пользователь не найден, пусть зарегистрируется (или что-то не так введено)\n{RequestCreditorNameString}");
                }
                RequestTarget(message.Chat.Id);
                return true;
            }
            catch (ScenarioLogicalException exc)
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, exc.Message);
                return false;
            }
        }

        private async Task<bool> OnTargetReceived(Message message)
        {
            try
            {
                var manager = DebtManager;
                var target = GetUserFromMessage(message, Context.PossibleTargets.Select(u => u.User));
                if (target == null)
                {
                    throw new ScenarioLogicalException("Такой пользователь не найден, пусть зарегистрируется (или что-то не так введено)");
                }

                var isCreditor = Context.PossibleTargets.First(u => u.User.Id == target.Id).IsCreditor;
                IEnumerable<Model.Debt> debts;
                if (isCreditor)
                {
                    debts = manager.GetActiveDebts(Context.Requester.Name, target.Name);
                }
                else
                {
                    debts = manager.GetActiveDebts(target.Name, Context.Requester.Name);
                }
                var builder = new StringBuilder();
                foreach (var debt in debts)
                {
                    builder.Append(
                        $"*Долг: {debt.LeftSum}*\nДобавлен: {debt.TimeStamp}\nОписание: {debt.Description}\n\n");
                }
                await BotClient.SendTextMessageAsync(message.Chat.Id, builder.ToString(), ParseMode.Markdown);
                return true;
            }
            catch (ScenarioLogicalException exc)
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, exc.Message);
                return false;
            }
        }
    }
}
