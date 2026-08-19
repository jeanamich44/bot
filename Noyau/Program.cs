using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot;
using ChezRheyyBot;
using Telegram.Bot.Types.ReplyMarkups;

public class TelegramHttpMetricsHandler : DelegatingHandler
{
    public TelegramHttpMetricsHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        config.IncTelegramSent();
        return await base.SendAsync(request, cancellationToken);
    }
}

class Program
{
    private static CancellationTokenSource? _pollingCts;
    private static CancellationTokenSource? _sumupPollingCts;
    private static readonly object _modeLock = new object();
    private static readonly object _idMessageLock = new object();

    static async Task Main(string[] args)
    {
        var handler = new TelegramHttpMetricsHandler(new HttpClientHandler());
        using var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(65)
        };
        var botClient = new TelegramBotClient(config.botToken, httpClient);

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        DataBase.CreerTableStockSiExistePas();
        await config.ReadJson();
        config.GetProfileSettings();

        var me = await botClient.GetMeAsync();
        Console.WriteLine($"Bot {me.Username} est dÃ©marrÃ©...");

        try
        {
            await botClient.SetMyCommandsAsync(new[]
            {
                new BotCommand { Command = "start", Description = "DÃ©marrage" }
            }, cancellationToken: cancellationToken);
        }
        catch { }

        Task serveurWebTask = ServeurWeb.LancerServeurWebAdmin(botClient, cts.Token);
        await Task.Delay(200, cancellationToken); // Laisser le temps au serveur web de s'initialiser

        await AppliquerModeTelegram(botClient, config.ModeTelegram);
        AppliquerModeSumUp(botClient, config.ModeSumUp);
        Task verifierTask = paiement.VerifierPaiement(botClient, cts.Token);

        _ = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(15000, cts.Token);
                    DataBase.SauvegarderSettings();
                }
                catch { }
            }
        }, cts.Token);

        try { await Task.Delay(Timeout.Infinite, cancellationToken); } catch (OperationCanceledException) { }

        config.JsonWrite();
        config.SetProfileSettings();
        cts.Cancel();
    }

    public static async Task AppliquerModeTelegram(ITelegramBotClient botClient, string targetMode)
    {
        string mode = config.NormaliserModeReception(targetMode);
        config.ModeTelegram = mode;

        if (mode == "webhook")
        {
            lock (_modeLock)
            {
                if (_pollingCts != null)
                {
                    _pollingCts.Cancel();
                    _pollingCts.Dispose();
                    _pollingCts = null;
                }
            }

            string domainEnv = config.DomainePublic();
            string webhookUrl = $"https://{domainEnv}/webhook/telegram/";
            using (var webhookClient = new HttpClient())
            {
                var form = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["url"] = webhookUrl,
                    ["secret_token"] = config.TelegramWebhookSecret
                });
                var webhookResp = await webhookClient.PostAsync($"https://api.telegram.org/bot{config.botToken}/setWebhook", form);
                if (!webhookResp.IsSuccessStatusCode)
                {
                    string body = await webhookResp.Content.ReadAsStringAsync();
                    Console.WriteLine($"[Telegram Mode] setWebhook échec: {(int)webhookResp.StatusCode}");
                }
            }
            Console.WriteLine($"[Telegram Mode] Mode Webhook: {webhookUrl} (secret token actif)");
        }
        else
        {
            try
            {
                await botClient.DeleteWebhookAsync();
            }
            catch { }

            lock (_modeLock)
            {
                if (_pollingCts == null)
                {
                    _pollingCts = new CancellationTokenSource();
                    var receiverOptions = new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };
                    botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, _pollingCts.Token);
                    Console.WriteLine("[Telegram Mode] Mode Polling: ActivÃ©");
                }
            }
        }
    }

    public static void AppliquerModeSumUp(ITelegramBotClient botClient, string targetMode)
    {
        string mode = config.NormaliserModeReception(targetMode);
        config.ModeSumUp = mode;

        lock (_modeLock)
        {
            if (mode == "webhook")
            {
                if (_sumupPollingCts != null)
                {
                    _sumupPollingCts.Cancel();
                    _sumupPollingCts.Dispose();
                    _sumupPollingCts = null;
                }
                string domainEnv = config.DomainePublic();
                Console.WriteLine($"[SumUp Mode] Mode Webhook: https://{domainEnv}/webhook/sumup/");
            }
            else
            {
                if (_sumupPollingCts == null)
                {
                    _sumupPollingCts = new CancellationTokenSource();
                    _ = paiement.VerifierPaiementSumAPI(botClient, _sumupPollingCts.Token);
                    Console.WriteLine("[SumUp Mode] Mode Polling: Activé");
                }
            }
        }
    }

    public static async Task AnnoncerModeMaintenance(ITelegramBotClient botClient, bool maintenance, CancellationToken cancellationToken = default)
    {
        string msg = maintenance
            ? "ðŸ› ï¸ <b>Mode Maintenance ActivÃ©</b>\n\nLe bot est actuellement en maintenance. Certaines fonctionnalitÃ©s peuvent Ãªtre indisponibles temporairement.\n\nðŸ’¬ <b>Besoin d'Aide ? Contactez un Admin :</b>\n@RheyyFondaa\n@NtRheyyTech"
            : "ðŸŸ¢ <b>Mode Maintenance DÃ©sactivÃ©</b>\n\nLe bot est de nouveau entiÃ¨rement opÃ©rationnel ! Merci de votre patience. ðŸŽ‰";

        var users = config.CopierUtilisateurs();
        foreach (var item in users)
        {
            try
            {
                await botClient.SendTextMessageAsync(item.Id, msg, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
                await Task.Delay(35, cancellationToken);
            }
            catch { }
        }
    }

    public static async Task TraiterUpdateWebhook(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        await HandleUpdateAsync(botClient, update, cancellationToken);
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            config.IncTelegramReceived();
            config.ResetContexte();

            if (!ExtraireContexte(update))
            {
                Console.WriteLine("[-] Erreur recuperation ChatId");
                return;
            }

            string chatId = config.CurrentChatId;

            if (config.BanniUser.Contains(chatId))
            {
                long.TryParse(chatId, out long bId);
                string reasonText = config.BanReasons.TryGetValue(bId, out string? r) && !string.IsNullOrEmpty(r) ? $"\nRaison : {r}" : "";
                await botClient.SendTextMessageAsync(chatId, $"Vous avez Ã©tÃ© banni de @ChezRheyyBot.{reasonText}\nEn cas de besoin contactez un administrateur.");
                return;
            }

            if (config.ModeMaintenance && !config.idAdmins.Contains(chatId))
            {
                await botClient.SendTextMessageAsync(chatId, "ðŸ› ï¸ <b>Maintenance en cours</b>\n\nLe bot est actuellement en maintenance. Veuillez rÃ©essayer ultÃ©rieurement.\n\nðŸ’¬ <b>Besoin d'Aide ? Contactez un Admin :</b>\n@RheyyFondaa\n@NtRheyyTech", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                return;
            }

            if (config.blockstart == false)
            {
                _ = Blocks.LancerActionDans24h(botClient, update, cancellationToken);
                config.blockstart = true;
            }

            lock (_idMessageLock)
            {
                config.IdMessage[chatId] = config.msgId;
            }

            config.IncCommandsExecuted();

            if (long.TryParse(chatId, out long newUserId))
            {
                config.ObtenirOuCreerUtilisateur(newUserId);
                DataBase.SauvegarderUtilisateurIndividuel(newUserId);
            }

            if (update.Type == UpdateType.Message && update.Message is { } message)
            {
                string textToProcess = message.Text ?? message.Caption ?? "";
                if (config.idAdmins.Contains(chatId))
                {
                    if (!string.IsNullOrWhiteSpace(textToProcess) && await admin.CommandeAdmin(textToProcess, botClient, update, cancellationToken))
                    {
                        return;
                    }
                    if (textToProcess == "/start")
                    {
                        await SampleM.SendMessage(botClient, update, cancellationToken);
                        return;
                    }
                }
                else if (textToProcess == "/start")
                {
                    await SampleM.SendMessage(botClient, update, cancellationToken);
                    return;
                }

                if (!string.IsNullOrEmpty(message.Text) && await TraiterSaisieMontant(botClient, update, cancellationToken, message.Text, chatId))
                {
                    return;
                }
                return;
            }
            else if (update.Type == UpdateType.CallbackQuery)
            {
                await callBackR.ResponseCallBack(botClient, update, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Erreur Globale] {ex.Message}");
        }
    }

    static async Task<bool> TraiterSaisieMontant(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken, string texte, string chatId)
    {
        if (config.CustomPaiement.ContainsKey(chatId))
        {
            if (!int.TryParse(texte, out int mtn))
            {
                await botClient.SendTextMessageAsync(chatId, "Veuillez ne fournir que des chiffres.");
                return true;
            }

            if (mtn < 3 || mtn > 70)
            {
                await botClient.SendTextMessageAsync(chatId, "âš ï¸ <b>Montant invalide.</b> Le montant d'une recharge Crypto doit Ãªtre compris entre <b>3 â‚¬</b> et <b>70 â‚¬</b>.", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                return true;
            }

            Console.WriteLine($"[Paiement] Demande rechargement Crypto manuel: {mtn}â‚¬ par {chatId}");
            config.CustomPaiement.TryRemove(chatId, out _);
            await paiement.GenerateLink(botClient, update, cancellationToken, texte);
            return true;
        }

        if (config.AttentePaiement.ContainsKey(chatId))
        {
            if (!int.TryParse(texte, out int mtn))
            {
                await botClient.SendTextMessageAsync(chatId, "Veuillez ne fournir que des chiffres.");
                return true;
            }

            if (mtn <= 0)
            {
                await botClient.SendTextMessageAsync(chatId, $"Impossible de recharger {texte}\n");
                return true;
            }
            if (mtn > 70)
            {
                await botClient.SendTextMessageAsync(chatId, "Impossible de recharger au dessus de 70 euros.");
                return true;
            }

            config.AttentePaiement.TryRemove(chatId, out _);
            await paiement.CreerPaiementSumAPI(botClient, update, cancellationToken, mtn);
            return true;
        }

        return false;
    }

    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            config.IncErrorsCount();
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException =>
                    $"Erreur API Telegram:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(errorMessage);
        }
        catch { }

        return Task.CompletedTask;
    }

    static bool ExtraireContexte(Update update)
    {
        try
        {
            long id = 0;
            string pseudo = "";
            string messageId = "";

            if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery?.From != null)
            {
                id = update.CallbackQuery.From.Id;
                pseudo = update.CallbackQuery.From.Username ?? update.CallbackQuery.From.FirstName ?? "";
                if (update.CallbackQuery.Message != null) messageId = update.CallbackQuery.Message.MessageId.ToString();
            }
            else if (update.Type == UpdateType.Message && update.Message?.From != null)
            {
                id = update.Message.From.Id;
                pseudo = update.Message.From.Username ?? update.Message.From.FirstName ?? "";
                messageId = update.Message.MessageId.ToString();
            }
            else if (update.Type == UpdateType.InlineQuery && update.InlineQuery?.From != null)
            {
                id = update.InlineQuery.From.Id;
                pseudo = update.InlineQuery.From.Username ?? update.InlineQuery.From.FirstName ?? "";
                messageId = update.InlineQuery.Id;
            }
            else
            {
                return false;
            }

            config.CurrentChatId = id.ToString();
            config.CurrentPseudo = pseudo;
            config.msgId = messageId;

            string formattedUname = !string.IsNullOrWhiteSpace(pseudo) ? (pseudo.StartsWith("@") ? pseudo : "@" + pseudo) : "";
            lock (config.UsersLock)
            {
                if (!string.IsNullOrEmpty(formattedUname) || !config.Usernames.ContainsKey(id))
                {
                    config.Usernames[id] = formattedUname;
                }
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
