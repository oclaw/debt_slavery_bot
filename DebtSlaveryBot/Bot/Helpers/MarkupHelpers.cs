using Telegram.Bot.Types.ReplyMarkups;

namespace DebtSlaveryBot.Bot.Helpers
{
    class MarkupHelpers
    {
        public const string YesText = "Да";
        public const string NoText = "Нет";

        public static ReplyKeyboardMarkup YesNoButtonTemplate => new ReplyKeyboardMarkup(
                new KeyboardButton[] { YesText, NoText }
        ) { ResizeKeyboard = true };
    }
}
