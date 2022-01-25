using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;


using SimpleExpressionEvaluator;

namespace DebtSlaveryBot.Bot.Scenario
{
    // Base class with helper methods for scenarios that includes both user and events model types (e.g. AddDebts, ShareDebt)
    class TgBotUserEventScenario : TelegramBotScenario
    {
        public TgBotUserEventScenario(ILogger<IBotService> logger, IServiceProvider serviceProvider)
            : base(logger, serviceProvider)
        { }

        protected string BuildUserListFromEvent(string prefix, Model.DebtEvent _event, Model.User skipUser, string postfix)
        {
            var builder = new StringBuilder();
            int n = 1;

            if (prefix != null)
                builder.AppendLine(prefix);

            foreach (var borrower in _event.Users.Where(u => u != skipUser))
            {
                builder.Append($"{n++}. {borrower.TgName}\n");
            }
            
            if (postfix != null)
                builder.Append(postfix);
    
            return builder.ToString();
        }

        private Model.User GetUserByIdx(string idxText, Model.DebtEvent _event, Model.User skipUser)
        {
            if (!int.TryParse(idxText, out int idx))
            {
                Logger.LogWarning("Cannot extract index!");
                return null;
            }
            idx--;

            int maxCount = _event.Users.Count;
            if (skipUser != null)
            {
                maxCount--;
            }

            if (idx < 0 || idx >= maxCount)
            {
                Logger.LogWarning($"Bad index ({idx} vs {maxCount})");
                return null;
            }

            if (skipUser != null)
                return _event.Users.Where(u => u != skipUser).Skip(idx).First();

            return _event.Users.Skip(idx).First();
        }

        protected List<Model.User> GetUserListFromMessage(Message message, Model.DebtEvent _event, Model.User skipUser)
        {
            if (message.Type != MessageType.Text)
            {
                return new List<Model.User> { GetUserFromMessage(message, _event, skipUser) };
            }

            if (_event == null)
            {
                Logger.LogWarning("Null event is not supported for multiple user selection");
                return null;
            }

            List<string> separatorMatches = new() { "\\.", ",", "\\s" };
            if (!separatorMatches.Any(m => Regex.IsMatch(message.Text, $"^(\\d+{m}*)+$")))
            {
                Logger.LogWarning("Cannot identify string as index sequence");
                return null;
            }

            var res = new List<Model.User>();
            foreach (var number in Regex.Matches(message.Text, "\\d+").Select(m => m.Value))
            {
                if (GetUserByIdx(number, _event, skipUser) is var user && user == null)
                {
                    Logger.LogWarning($"Cannot get user from index entry {number}");
                    return null;
                }
                res.Add(user);
            }

            return res;
        }

        protected Model.User GetUserFromMessage(Message message, Model.DebtEvent _event, Model.User skipUser)
        {
            var manager = DebtManager;
            var parser = new Helpers.UserNameParser(manager);
            var result = parser.Parse(message);
            if (result != null)
            {
                if (_event != null && !_event.Users.Contains(result))
                {
                    return null;  // user not found in current event list, invalidating search result
                }
                return result;
            }

            Logger.LogInformation("Direct user name is not parsed, searching through event");
            if (_event == null)
            {
                Logger.LogWarning("Event is null, cannot parse!");
                return null;
            }

            if (message.Type != MessageType.Text)
            {
                Logger.LogWarning("Cannot extract index from non-text message!");
                return null;
            }

            return GetUserByIdx(message.Text, _event, skipUser);
        }

        protected decimal CalcSum(string text, string errorText)
        {
            decimal result;
            try
            {
                if (text.Contains(','))
                    throw new ArgumentException("no commas allowed");

                if (!decimal.TryParse(text, out result))
                {
                    var engine = new ExpressionEvaluator();
                    result = engine.Evaluate(text);
                }
                result = Math.Round(result, 2);
            }
            catch (Exception exc)
            {
                Logger.LogDebug($"AddDebtScenario: Failed to calculate debt sum ({exc.Message}), text is '{text}'");
                throw new ScenarioLogicalException(errorText);
            }
            if (result <= 0)
            {
                throw new ScenarioLogicalException(errorText);
            }
            return result;
        }
    }
}
