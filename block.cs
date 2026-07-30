using System;
using Telegram.Bot.Types;
using Telegram.Bot;

namespace UgcBotTG
{
    internal class Blocks
    {
        public static async Task LancerActionDans24h(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(20));

                foreach(var id in config.banAPI)
                {
                    await botClient.SendTextMessageAsync(id, $"Cooldown finit, vous pouvez désormais créer un nouveau lien de paiement");
                }

                config.banAPI.Clear();

                string Solde = "444";

                if(Solde.Length == 4)
                {
                    Solde = Solde.Substring(0, 2);
                }else if(Solde.Length == 3)
                {
                    Solde = Solde.Substring(0, 1);
                }


                Solde = Solde.Substring(0, 2);

                return;
            }
        }
    }
}
