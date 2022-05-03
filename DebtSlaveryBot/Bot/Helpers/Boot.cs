using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DebtSlaveryBot.Bot.Helpers.Attributes;
using System.Text;
using System.Threading.Tasks;

namespace DebtSlaveryBot.Bot.Helpers
{
    internal static class Boot
    {
        public static IEnumerable<Type> GetActiveCommands(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
            {
                var attrs = type.GetCustomAttributes(typeof(BotCommandAttribute), true);
                if (attrs.Length > 0 && (attrs.First() as BotCommandAttribute).Active)
                {
                    yield return type;
                }
            }
        }
    }
}
