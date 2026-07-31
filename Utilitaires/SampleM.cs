using Telegram.Bot.Types;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;

namespace ChezRheyyBot
{
    internal class SampleM
    {
        private static string photoUrl = "https://i.ibb.co/Mkzs1vzc/IMG-1675.jpg";

        public static async Task SendMessage(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                int result = config.UserSave.FindIndex(tuple => tuple.Item1 == long.Parse(config.CurrentChatId));
                var ancienTuple = config.UserSave[result];

                var keyboardButtons = new List<List<InlineKeyboardButton>>
                {
                    new List<InlineKeyboardButton>
                    {
                        InlineKeyboardButton.WithCallbackData("📺 IPTV","iIPTV"),
                        InlineKeyboardButton.WithCallbackData("🛒 Carrefour","iCarrefour"),
                    },
                    new List<InlineKeyboardButton>
                    {
                        InlineKeyboardButton.WithCallbackData("💳 Paiement", "iPaiement"),
                        InlineKeyboardButton.WithCallbackData("👤 Profil", "iProfile")
                    }
                };

                var inlineKeyboard = new InlineKeyboardMarkup(keyboardButtons);
                await botClient.SendPhotoAsync(config.CurrentChatId, photoUrl, caption: $"<strong>Bienvenue sur @ChezRheyy Bot</strong>\n\n🆔 <code>{config.CurrentChatId}</code>\n💰 {ancienTuple.Item3}€", parseMode: ParseMode.Html, replyMarkup: inlineKeyboard);
                return;
            }
            catch
            {
                int result = config.UserSave.FindIndex(tuple => tuple.Item1 == long.Parse(config.CurrentChatId));
                if(result == -1)
                {
                    return;
                }

                var ancienTuple = config.UserSave[result];

                var keyboardButtons = new List<List<InlineKeyboardButton>>
                {
                    new List<InlineKeyboardButton>
                    {
                        InlineKeyboardButton.WithCallbackData("📺 IPTV","iIPTV"),
                        InlineKeyboardButton.WithCallbackData("🛒 Carrefour","iCarrefour")
                    },
                    new List<InlineKeyboardButton>
                    {
                        InlineKeyboardButton.WithCallbackData("💳 Paiement", "iPaiement"),
                        InlineKeyboardButton.WithCallbackData("👤 Profil", "iProfile")
                    }
                };

                var inlineKeyboard = new InlineKeyboardMarkup(keyboardButtons);
                await botClient.SendPhotoAsync(config.CurrentChatId, photoUrl, caption: $"<strong>Bienvenue sur @ChezRheyy Bot</strong>\n\n🆔 <code>{config.CurrentChatId}</code>\n💰 {ancienTuple.Item3}€", parseMode: ParseMode.Html, replyMarkup: inlineKeyboard);
                return;
            }
        }
    }
}
