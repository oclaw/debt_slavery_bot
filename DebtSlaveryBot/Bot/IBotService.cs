using System.Threading;
using DebtSlaveryBot.Bot.Scenario;

namespace DebtSlaveryBot.Bot
{ 
    interface IBotService
    {
        public void Start(CancellationToken cancellationToken);
        public void RunScenario(ChatEntry entry, TelegramBotScenario scenario);
        public void ResetScenario(ChatEntry entry);
        public (bool, long) GetPrimaryChatId(long tgUserId);
    }
}
