using System;
using Telegram.Bot.Types;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace ChezRheyyBot
{
    internal class profile
    {
        public static async Task AfficherProfile(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]) - 1);

                string croix = "";

                var inlineKeyboard = new InlineKeyboardMarkup(new[]
          {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("🛒 Carrefour", "iCarrefourProfile"),
                        InlineKeyboardButton.WithCallbackData("🍔 Quick","iQuickProfile")
                    },
                });


                await botClient.SendTextMessageAsync(config.CurrentChatId, $"Bonjour @{config.CurrentPseudo}\n\n",replyMarkup:inlineKeyboard);
            }
            catch
            {
                await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]));



            }
        }
    }
}
