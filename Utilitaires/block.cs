using Telegram.Bot;
using Telegram.Bot.Types;

namespace ChezRheyyBot
{
    internal class Blocks
    {
        public static async Task LancerActionDans24h(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                    config.PurgerCooldownsExpires();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cooldown Worker] {ex.Message}");
            }
        }
    }
}
