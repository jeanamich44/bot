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
                double solde = result != -1 ? config.UserSave[result].Item3 : 0.0;
                int achats = result != -1 ? config.UserSave[result].Item2 : 0;

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
                                 $"🆔 <code>{config.CurrentChatId}</code>\n" +
                                 $"💰 {solde}€\n" +
                                 $"🛒 {achats} commande(s)\n\n" +
                                 $"💬 <b>Besoin d'Aide ? Contactez un Admin :</b>\n" +
                                 $"@RheyyFondaa\n" +
                                 $"@NtRheyyTech";

                await botClient.SendPhotoAsync(config.CurrentChatId, photoUrl, caption: caption, parseMode: ParseMode.Html, replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
                return;
            }
            catch
            {
                int result = config.UserSave.FindIndex(tuple => tuple.Item1 == long.Parse(config.CurrentChatId));
                double solde = result != -1 ? config.UserSave[result].Item3 : 0.0;
                int achats = result != -1 ? config.UserSave[result].Item2 : 0;

                var keyboardButtons = new List<List<InlineKeyboardButton>>
                {
                    new List<InlineKeyboardButton>
                    {
                        InlineKeyboardButton.WithCallbackData("📺 IPTV","iIPTV"),
                        InlineKeyboardButton.WithCallbackData("🛒 Carrefour","iCarrefour")
                    },
                    new List<InlineKeyboardButton>
                    {
                        InlineKeyboardButton.WithCallbackData("💳 Paiement", "iPaiement")
                    }
                };

                var inlineKeyboard = new InlineKeyboardMarkup(keyboardButtons);
                string caption = $"<b>Bienvenue sur @ChezRheyy Bot</b>\n\n" +
                                 $"🆔 <code>{config.CurrentChatId}</code>\n" +
                                 $"💰 {solde}€\n" +
                                 $"🛒 {achats} commande(s)\n\n" +
                                 $"💬 <b>Besoin d'Aide ? Contactez un Admin :</b>\n" +
                                 $"@RheyyFondaa\n" +
                                 $"@NtRheyyTech";

                await botClient.SendPhotoAsync(config.CurrentChatId, photoUrl, caption: caption, parseMode: ParseMode.Html, replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
                return;
            }
        }
    }
}
