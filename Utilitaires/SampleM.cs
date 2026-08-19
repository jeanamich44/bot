using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;

namespace ChezRheyyBot
{
    internal class SampleM
    {
        private static string photoUrl = "https://i.ibb.co/Mkzs1vzc/IMG-1675.jpg";

        public static async Task SendMessage(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            string chatId = config.CurrentChatId;
            long.TryParse(chatId, out long userId);
            var user = config.TrouverUtilisateur(userId);
            double solde = user?.Solde ?? 0.0;
            int achats = user?.Achat ?? 0;

            var keyboardButtons = new List<List<InlineKeyboardButton>>
            {
                new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("📺 IPTV","iIPTV"),
                    InlineKeyboardButton.WithCallbackData("🛒 Carrefour","iCarrefour"),
                },
                new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("💳 Paiement", "iPaiement")
                }
            };

            var inlineKeyboard = new InlineKeyboardMarkup(keyboardButtons);
            string caption = $"<b>Bienvenue sur @ChezRheyy Bot</b>\n\n" +
                             $"🆔 <code>{chatId}</code>\n" +
                             $"💰 {solde}€\n" +
                             $"🛒 {achats} commande(s)\n\n" +
                             $"💬 <b>Besoin d'Aide ? Contactez un Admin :</b>\n" +
                             $"@RheyyFondaa\n" +
                             $"@NtRheyyTech";

            await botClient.SendPhotoAsync(chatId, photoUrl, caption: caption, parseMode: ParseMode.Html, replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
        }
    }
}
