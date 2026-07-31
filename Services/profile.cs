using System;
using Telegram.Bot.Types;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;

namespace ChezRheyyBot
{
    internal class profile
    {
        public static async Task AfficherProfile(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                int result = config.UserSave.FindIndex(tuple => tuple.Item1 == long.Parse(config.CurrentChatId));
                double solde = result != -1 ? config.UserSave[result].Item3 : 0.0;
                int achats = result != -1 ? config.UserSave[result].Item2 : 0;

                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("🏠 Accueil", "iHome")
                    }
                });

                string message = $"👤 *PROFIL UTILISATEUR*\n\n" +
                                 $"🆔 ID : `{config.CurrentChatId}`\n" +
                                 $"👤 Pseudo : @{config.CurrentPseudo}\n" +
                                 $"💰 Solde : {solde}€\n" +
                                 $"🛒 Achats effectués : {achats}\n\n" +
                                 $"💬 *Besoin Aide ? Contactez un Admin :*\n" +
                                 $"@RheyyFondaa\n" +
                                 $"@NtRheyyTech";

                try
                {
                    await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]) - 1, cancellationToken: cancellationToken);
                }
                catch { }

                await botClient.SendTextMessageAsync(config.CurrentChatId, message, parseMode: ParseMode.Markdown, replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
            }
            catch { }
        }
    }
}
