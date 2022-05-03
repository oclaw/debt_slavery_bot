using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebtSlaveryBot.Bot.Helpers.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    internal class BotCommandAttribute : Attribute
    {
        public bool Active { get; set; } = true;  // when false command is not added to bot message processing
    }
}
