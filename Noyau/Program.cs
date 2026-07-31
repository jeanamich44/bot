using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot;
using ChezRheyyBot;
using Telegram.Bot.Types.ReplyMarkups;

class Program
{
    static async Task Main(string[] args)
    {
        var botClient = new TelegramBotClient(config.botToken);

        // Configure le gestionnaire des mises à jour
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() // Recevoir tous les types d'update
        };

        botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken
        );



        DataBase.CreerTableStockSiExistePas();
        await config.ReadJson();
        config.InitialiseCategorie();
        config.GetProfileSettings();

        var me = await botClient.GetMeAsync();

        try
        {
            await botClient.SetMyCommandsAsync(new[]
            {
                new BotCommand { Command = "start", Description = "Démarrage" }
            }, cancellationToken: cancellationToken);
        }
        catch { }

        Console.WriteLine($"Bot {me.Username} est démarré...");
        Task verifierTask = paiement.VerifierPaiement(botClient, cts.Token);
        Task webhookSumUpTask = paiement.LancerServeurWebhookSumUp(botClient, cts.Token);

        await Task.Delay(Timeout.Infinite, cancellationToken);

        config.JsonWrite();
        config.SetProfileSettings();
        cts.Cancel();
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {


            if (!await GetChatId(botClient, update, cancellationToken))
            {
                Console.WriteLine("[-] Erreur recuperation ChatId");
                return;
            }


            if (config.BanniUser.Contains(config.CurrentChatId))
            {
                long.TryParse(config.CurrentChatId, out long bId);
                string reasonText = config.BanReasons.TryGetValue(bId, out string? r) && !string.IsNullOrEmpty(r) ? $"\nRaison : {r}" : "";
                await botClient.SendTextMessageAsync(config.CurrentChatId, $"Vous avez été banni de @ChezRheyyBot.{reasonText}\nEn cas de besoin contactez un administrateur.");
                return;
            }

            if(config.blockstart == false)
            {
                Blocks.LancerActionDans24h(botClient, update, cancellationToken);
                config.blockstart = true;
            }

            if (!await GetInformation(botClient, update, cancellationToken))
            {
                if(config.CurrentPseudo == "")
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "❌ Merci de configurer un pseudo pour utiliser le bot");
                    return;
                }

                return;
            }

            if (!config.IdMessage.ContainsKey(config.CurrentChatId))
            {
                config.IdMessage.Add(config.CurrentChatId, config.msgId);
            }
            else
            {
                config.IdMessage[config.CurrentChatId] = config.msgId;
            }




            int result = config.UserSave.FindIndex(tuple => tuple.Item1 == long.Parse(config.CurrentChatId));
            if (result == -1)
            {
                config.UserSave.Add(Tuple.Create(long.Parse(config.CurrentChatId), 0, 0.0, false));
            }

            if (update.Type == UpdateType.Message && update.Message is { } message && update.Message.Text == null)
            {
                //error
                return;
            }
            else if (update.Type == UpdateType.Message)
            {
                if (config.idAdmins.Contains(config.CurrentChatId))
                {
                    if (await admin.CommandeAdmin(update.Message.Text, botClient, update, cancellationToken))
                    {
                        return;
                    }
                    else if(update.Message.Text == "/start")
                    {
                        await SampleM.SendMessage(botClient, update, cancellationToken);
                        return;
                    }

                    else if(update.Message.Text != "" && config.CustomPaiement.Contains(config.CurrentChatId))
                    {
                        int mtn = 0;

                        if(!int.TryParse(update.Message.Text, out mtn))
                        {
                            await botClient.SendTextMessageAsync(config.CurrentChatId, "Veuillez ne fournir que des chiffres.");
                            return;
                        }

                        if (mtn <= 0)
                        {
                            await botClient.SendTextMessageAsync(config.CurrentChatId, $"Impossible de recharger {update.Message.Text}\n");
                            return;
                        }
                        
                        Console.WriteLine($"[Paiement] Demande rechargement Crypto manuel: {mtn}€ par {config.CurrentChatId}");
                        config.CustomPaiement.Remove(config.CurrentChatId);
                        await paiement.GenerateLink(botClient, update, cancellationToken, update.Message.Text);
                        return;
                    }
                    else if(update.Message.Text != "" && config.AttentePaiement.Contains(config.CurrentChatId))
                    {
                        int mtn = 0;

                        if (!int.TryParse(update.Message.Text, out mtn))
                        {
                            await botClient.SendTextMessageAsync(config.CurrentChatId, "Veuillez ne fournir que des chiffres.");
                            return;
                        }

                        if (mtn <= 0)
                        {
                            await botClient.SendTextMessageAsync(config.CurrentChatId, $"Impossible de recharger { update.Message.Text}\n");
                            return;
                        }
                        else if(mtn >= 70)
                        {
                            await botClient.SendTextMessageAsync(config.CurrentChatId, "Impossible de recharger au dessus de 70 euros.");
                            return;
                        }

                        await paiement.CreerPaiementSumAPI(botClient,update,cancellationToken,mtn);
                        return;
                    }
                }
                else
                {
                    if(update.Message.Text == "/start")
                    {
                        await SampleM.SendMessage(botClient, update, cancellationToken);
                        return;
                    }

                    else if (update.Message.Text != "" && config.CustomPaiement.Contains(config.CurrentChatId))
                    {

                        int mtn = 0;

                        if (!int.TryParse(update.Message.Text, out mtn))
                        {
                            await botClient.SendTextMessageAsync(config.CurrentChatId, "Veuillez ne fournir que des chiffres.");
                            return;
                        }

                        if (mtn <= 0)
                        {
                            await botClient.SendTextMessageAsync(config.CurrentChatId, $"Impossible de recharger {update.Message.Text}\n");
                            return;
                        }

                        Console.WriteLine($"[Paiement] Demande rechargement Crypto manuel: {mtn}€ par {config.CurrentChatId}");
                        await paiement.GenerateLink(botClient, update, cancellationToken, update.Message.Text);
                        return;
                    }
                    else if (update.Message.Text != "" && config.AttentePaiement.Contains(config.CurrentChatId))
                    {
                        int mtn = 0;

                        if (!int.TryParse(update.Message.Text, out mtn))
                        {
                            await botClient.SendTextMessageAsync(config.CurrentChatId, "Veuillez ne fournir que des chiffres.");
                            return;
                        }

                        if (mtn <= 0)
                        {
                            await botClient.SendTextMessageAsync(config.CurrentChatId, $"Impossible de recharger {update.Message.Text}\n");
                            return;
                        }
                        else if (mtn > 70)
                        {
                            await botClient.SendTextMessageAsync(config.CurrentChatId, "Impossible de recharger au dessus de 70 euros.");
                            return;
                        }

                        await paiement.CreerPaiementSumAPI(botClient, update, cancellationToken, mtn);
                        return;
                    }

                }
                return;
            }
            else if (update.Type == UpdateType.CallbackQuery)
            {
                await callBackR.ResponseCallBack(botClient, update, cancellationToken);
                return;
            }
        }
        catch
        {

        }

    }
    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException =>
                    $"Erreur API Telegram:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(errorMessage);
            return Task.CompletedTask;
        }
        catch
        {

        }

        return Task.CompletedTask;
    }
    static async Task<bool> GetChatId(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            if (update.Type == UpdateType.CallbackQuery)
            {
                config.CurrentChatId = update.CallbackQuery.From.Id.ToString();
                config.msgId = update.CallbackQuery.Message.MessageId.ToString();
                config.CurrentPseudo = update.CallbackQuery.From.Username;
                return true;
            }
            else if (update.Type == UpdateType.Message)
            {
                config.CurrentChatId = update.Message.From.Id.ToString();
                config.msgId = update.Message.MessageId.ToString();
                config.CurrentPseudo = update.Message.From.Username;
                return true;
            }
            else if (update.Type == UpdateType.InlineQuery)
            {
                config.CurrentChatId = update.InlineQuery.From.Id.ToString();
                config.msgId = update.InlineQuery.Id;
                config.CurrentPseudo = update.InlineQuery.From.Username;
                return true;
            }


        }
        catch
        {
            return false;
        }


        return false;
    }
    private static async Task<bool> GetInformation(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            if (update.Type == UpdateType.CallbackQuery)
            {
                if (update.CallbackQuery.From.Username == null)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Merci de configuerer un username avant d'utiliser ce bot");
                    return false;
                }

                return true;
            }
            if (update.Type == UpdateType.Message)
            {
                if (update.Message.From.Username == null)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Merci de configuerer un username avant d'utiliser ce bot");
                    return false;
                }
                return true;
            }
        }
        catch
        {
            Console.WriteLine($"Impossible de Verifier si {config.CurrentChatId} a un [username] de configurer");
            await botClient.SendTextMessageAsync(config.CurrentChatId, "Erreur: Impossible de verifier votres requete");
            return false;
        }

        return false;
    }
}