using System;
using Telegram.Bot.Types;
using Telegram.Bot;

namespace ChezRheyyBot
{
    internal class Blocks
    {
        public static async Task LancerActionDans24h(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromMinutes(20));

            foreach (var id in config.banAPI)
            {
                try
                {
                    await botClient.SendTextMessageAsync(id, $"Cooldown fini, vous pouvez désormais créer un nouveau lien de paiement");
                }
                catch { }
            }

            config.banAPI.Clear();
        }
    }
}
