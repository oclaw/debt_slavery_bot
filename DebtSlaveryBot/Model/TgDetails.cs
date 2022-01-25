using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace DebtSlaveryBot.Model
{
    public class TgDetails
    {
        public TgDetails()
        {

        }

        public TgDetails(Telegram.Bot.Types.User tgUser)
        {
            Id = tgUser.Id;
            FirstName = tgUser.FirstName;
            LastName = tgUser.LastName;
            UserName = tgUser.Username;
        }

        [Key]
        public long Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string UserName { get; set; }
        public long PrivateChatId { get; set; }
        public bool ImpersonalMode { get; set; } = false;
    }
}
