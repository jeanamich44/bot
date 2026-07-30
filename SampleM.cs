using Telegram.Bot.Types;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;

namespace UgcBotTG
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

               // await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]) - 1);

                var keyboardButtons = new List<List<InlineKeyboardButton>>
                    {
                        new List<InlineKeyboardButton>
                        {
                            InlineKeyboardButton.WithCallbackData("🍔 Quick", "iQuick"),
                            InlineKeyboardButton.WithCallbackData("🍴​ Flunch","iFlunch"),

                        }
                    };
                keyboardButtons.Add(new List<InlineKeyboardButton>
                                        {
                                         InlineKeyboardButton.WithCallbackData("📺 IPTV","iIPTV"),
                                         InlineKeyboardButton.WithCallbackData("🛒 Carrefour","iCarrefour"),
                                    });

                keyboardButtons.Add(new List<InlineKeyboardButton>
                                        {
                                         InlineKeyboardButton.WithCallbackData("💳 Paiement", "iPaiement")
                                    });

                var inlineKeyboard = new InlineKeyboardMarkup(keyboardButtons);
                string valeur = "";
               
                await botClient.SendPhotoAsync(config.CurrentChatId, photoUrl, caption: $"<strong>Bienvenue sur @ChezRheyy Bot</strong>\n\n🆔 <code>{config.CurrentChatId}</code>\n💰 {ancienTuple.Item3}€\n🏷️ {valeur}", parseMode: ParseMode.Html, replyMarkup: inlineKeyboard);
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

               // await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]));

                var keyboardButtons = new List<List<InlineKeyboardButton>>
{
   new List<InlineKeyboardButton>
                        {
                            InlineKeyboardButton.WithCallbackData("🍔 Quick", "iQuick"),
                            InlineKeyboardButton.WithCallbackData("🍴​ Flunch","iFlunch"),

                        }
                    };
                keyboardButtons.Add(new List<InlineKeyboardButton>
                                        {
                                         InlineKeyboardButton.WithCallbackData("📺 IPTV","iIPTV"),
                                         InlineKeyboardButton.WithCallbackData("🛒 Carrefour","iCarrefour")
                                    });

                keyboardButtons.Add(new List<InlineKeyboardButton>
                                        {
                                         InlineKeyboardButton.WithCallbackData("💳 Paiement", "iPaiement")
                                    });

                var inlineKeyboard = new InlineKeyboardMarkup(keyboardButtons);
                string valeur = "";
                
                await botClient.SendPhotoAsync(config.CurrentChatId, photoUrl, caption: $"<strong>Bienvenue sur @ChezRheyy Bot</strong>\n\n🆔 <code>{config.CurrentChatId}</code>\n💰 {ancienTuple.Item3}€\n 🏷️ {valeur}", parseMode: ParseMode.Html, replyMarkup: inlineKeyboard);
                return;
            }
        }


        // gerer le code parrainage

        public static async Task CodeParrainage(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                config.WaitingParain.Remove(config.CurrentChatId);

                if (config.ParainUser.ContainsKey(config.CurrentChatId))
                {
                    if (config.ParainConfig.ContainsKey(update.Message.Text))
                    {
                        config.ParainUser[config.CurrentChatId] = update.Message.Text;
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"Le code [{update.Message.Text}] a bien ete ajouter");
                        return;
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"Le code [{update.Message.Text}] n'es pas valide");
                        return;
                    }
                }
                else
                {
                    if (config.ParainConfig.ContainsKey(update.Message.Text))
                    {
                        config.ParainUser.Add(config.CurrentChatId, update.Message.Text);

                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"Le code [{update.Message.Text}] a bien ete ajouter");
                        return;
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"Le code [{update.Message.Text}] n'es pas valide");
                        return;
                    }
                }
            }
            catch
            {

            }
        }

        public static async Task PreparationCodeParain(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                await botClient.SendTextMessageAsync(config.CurrentChatId, "Merci de rentrer votre code parrainage");
                config.WaitingParain.Add(config.CurrentChatId);
            }
            catch
            {

            }
        }
        public static async Task RemoveParain(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                config.ParainUser.Remove(config.CurrentChatId);

                await botClient.AnswerCallbackQueryAsync(
    callbackQueryId: update.CallbackQuery.Id,
    text: "Code parrainage retiré",
    showAlert: true
);
            }
            catch
            {

            }
        }

        public static async Task ResponseCinema(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var inlineKeyboard = new InlineKeyboardMarkup(new[]
          {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("🎬 PatheGaumont", "iPatheGaumont"),
                        InlineKeyboardButton.WithCallbackData("🎬 UGC","iUgc")
                    },
                });


                await botClient.SendTextMessageAsync(config.CurrentChatId, $"Trouvez le cinéma de votre choix", replyMarkup: inlineKeyboard);
            }
            catch
            {

            }
        }
    }
}
