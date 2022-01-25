using System;
using System.Collections.Generic;
using System.Text;

namespace DebtSlaveryBot.Bot.Scenario
{
    class ScenarioLogicalException : Exception
    {
        public ScenarioLogicalException(string messageForUser)
            : base(messageForUser)
        { }
    }
}
