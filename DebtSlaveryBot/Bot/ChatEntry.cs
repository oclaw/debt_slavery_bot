using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DebtSlaveryBot.Bot
{
    struct ChatEntry
    {
        public ChatEntry(Message message)
        {
            UserId = message.From.Id;
            ChatId = message.Chat.Id;
            Primary = message.Chat.Type is ChatType.Private;
        }

        public ChatEntry(long user, long chat, bool primary)
        {
            UserId = user;
            ChatId = chat;
            Primary = primary;
        }

        public long UserId;
        public long ChatId;
        public bool Primary;
    }
}
