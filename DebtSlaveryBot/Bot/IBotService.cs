using System;
using System.Collections.Generic;
using System.Text;

using System.Threading;

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

    interface IBotService
    {
        public void Start(CancellationToken cancellationToken);
        public void RunScenario(ChatEntry entry, Scenario.TelegramBotScenario scenario);
        public void ResetScenario(ChatEntry entry);
        public (bool, long) GetPrimaryChatId(long tgUserId);
    }
}
