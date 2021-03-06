using System;
using System.Collections.Generic;
using System.Text;

using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DebtSlaveryBot.Bot.Helpers
{
    class UserNameParser
    {
        public UserNameParser(Model.IDebtManager manager)
        {
            DebtManager = manager;
        }

        private Model.User GetUser(Contact contact)
        {
            var id = contact.UserId;
            if (id == null)
                throw new ArgumentException("contact.id");

            var user = DebtManager.GetUser(id.Value);
            return user;
        }

        private Model.User GetUser(Message message)
        {
            if (!message.Text.StartsWith('@'))
            {
                return null;
            }
            var uname = message.Text[1..];
            var user = DebtManager.GetUser(uname);
            return user;
        }

        public Model.User Parse(Message message) => message.Type switch
        {
            MessageType.Text => GetUser(message),
            MessageType.Contact => GetUser(message.Contact),
            _ => null
        };

        private Model.IDebtManager DebtManager;
    }
}
