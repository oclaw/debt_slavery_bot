using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using Telegram.Bot;

namespace DebtSlaveryBot.Bot.Scenario
{
    class TelegramBotScenario
    {
        protected delegate Task<bool> StepExecutor(Message msg);

        private LinkedList<StepExecutor> ScenarioChain;
        private LinkedListNode<StepExecutor> CurrentExecutor;

        protected ILogger<IBotService> Logger { get; private set; }
        protected ITelegramBotClient BotClient { get; private set; }
        protected IBotService BotService => Global.Services.GetService<IBotService>();
        protected Message CurrentMessage { get; private set; }  // TODO: replace method parameter with property

        public bool Completed => CurrentExecutor == null;

        protected TelegramBotScenario(ILogger<IBotService> logger, ITelegramBotClient botClient)
        {
            Logger = logger;
            BotClient = botClient;
            ScenarioChain = new LinkedList<StepExecutor>();
        }

        protected void ScheduleNext(StepExecutor executor)
        {
            ScenarioChain.AddLast(executor);
            if (CurrentExecutor == null)
            {
                CurrentExecutor = ScenarioChain.First;
            }
        }

        protected void SetChain(params StepExecutor[] methods)
        {
            if (methods.Length == 0)
            {
                throw new ArgumentException(null, nameof(methods));
            }

            ScenarioChain = new LinkedList<StepExecutor>();
            foreach (var m in methods)
            {
                ScenarioChain.AddLast(m);
            }
            CurrentExecutor = ScenarioChain.First;
        }

        public async Task<bool> Execute(Message tgMessage)
        {
            var executor = CurrentExecutor.Value;
            CurrentMessage = tgMessage;

            var result = await executor(tgMessage);
            Logger.LogDebug($"TelegramBotScenario step execution result={result}");
            if (result)
            {
                CurrentExecutor = CurrentExecutor.Next;
                if (CurrentExecutor == null)
                {
                    Logger.LogDebug($"TelegramBotScenarioBase all steps executed");
                    // todo end scenario?
                }
            }

            CurrentMessage = null;
            return Completed;
        }
    }
}
