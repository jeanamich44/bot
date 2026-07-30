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
