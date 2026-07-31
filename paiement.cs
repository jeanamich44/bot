using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ChezRheyyBot
{
    internal class paiement
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task PaiementList(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("💳 Paiement Carte Bancaire", "iCustomCB")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("₿ Paiement Cryptomonnaie", "iCustomP"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🏠 Accueil", "iHome")
                }
            });

            string text = "<b>💳 RECHARGEMENT DU SOLDE</b>\n\n" +
                          "Choisissez votre mode de paiement sécurisé ci-dessous :\n" +
                          "• <b>Cryptomonnaie</b> (Validation rapide, 30 min)\n" +
                          "• <b>Carte Bancaire</b> (Validation instantanée)\n";

            try
            {
                await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]) - 1);
                await botClient.SendTextMessageAsync(config.CurrentChatId, text, replyMarkup: inlineKeyboard, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
            }
            catch
            {
                try
                {
                    await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]));
                    await botClient.SendTextMessageAsync(config.CurrentChatId, text, replyMarkup: inlineKeyboard, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                }
                catch
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, text, replyMarkup: inlineKeyboard, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                }
            }
        }

        public static async Task RecupererMontant(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            string chatId = config.CurrentChatId;
            if (string.IsNullOrEmpty(chatId)) return;

            Console.WriteLine($"[Paiement] Menu sélection montant affiché pour ChatID: {chatId}");

            if (DataBase.AUnPaiementEnAttenteBDD(chatId))
            {
                var cancelKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("❌ Annuler ma facture en attente", "iCancelPaiement") },
                    new[] { InlineKeyboardButton.WithCallbackData("🏠 Accueil", "iHome") }
                });

                await botClient.SendTextMessageAsync(chatId, "⚠️ <b>Vous avez déjà un paiement en attente.</b>\n\nVeuillez finaliser votre paiement ou l'annuler pour en créer un nouveau.", replyMarkup: cancelKeyboard, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                return;
            }

            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("💵 10 €", "iPayAmt_10"),
                    InlineKeyboardButton.WithCallbackData("💵 20 €", "iPayAmt_20")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("✏️ Saisir un montant personnalisé", "iMontantPersoCrypto")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🏠 Accueil", "iHome")
                }
            });

            await botClient.SendTextMessageAsync(chatId, "<b>₿ PAIEMENT CRYPTOMONNAIE</b>\n\nSélectionnez un montant rapide ou saisissez le montant de votre choix :", replyMarkup: inlineKeyboard, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
        }

        public static async Task ActiverSaisieCustom(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            string chatId = config.CurrentChatId;
            if (string.IsNullOrEmpty(chatId)) return;

            Console.WriteLine($"[Paiement] Activation de la saisie personnalisée pour ChatID: {chatId}");

            if (!config.CustomPaiement.Contains(chatId))
            {
                config.CustomPaiement.Add(chatId);
            }
            await botClient.SendTextMessageAsync(chatId, "✏️ <b>Veuillez inscrire le montant souhaité en € :</b>\n<i>(Exemple: 15, 25, 35)</i>", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
        }

        public static async Task AnnulerPaiement(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            string chatId = config.CurrentChatId;
            if (string.IsNullOrEmpty(chatId)) return;

            Console.WriteLine($"[Paiement] Demande d'annulation de facture en attente pour ChatID: {chatId}");

            bool annule = DataBase.AnnulerPaiementEnAttenteBDD(chatId);
            if (annule)
            {
                try
                {
                    config.CustomPaiement.Remove(chatId);
                    config.AttentePaiement.Remove(chatId);
                }
                catch { }

                Console.WriteLine($"[Paiement OK] Facture en attente annulée avec succès pour ChatID: {chatId}");
                await botClient.SendTextMessageAsync(chatId, "✅ <b>Votre facture en attente a été annulée.</b> Vous pouvez à présent créer une nouvelle facture.", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
            }
            else
            {
                Console.WriteLine($"[Paiement] Aucune facture en attente trouvée à annuler pour ChatID: {chatId}");
                await botClient.SendTextMessageAsync(chatId, "ℹ️ Aucune facture en attente à annuler.");
            }
        }

        public static async Task<string> GenerateLink(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken, string montant = "")
        {
            string chatId = config.CurrentChatId;
            if (string.IsNullOrEmpty(chatId)) return "";

            Console.WriteLine($"[Paiement Crypto] Génération facture demandée par ChatID: {chatId}, Montant brut: '{montant}'");

            if (DataBase.AUnPaiementEnAttenteBDD(chatId))
            {
                Console.WriteLine($"[Paiement Crypto] Facture refusée: ChatID {chatId} a déjà un paiement en attente");
                var cancelKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("❌ Annuler ma facture en attente", "iCancelPaiement") },
                    new[] { InlineKeyboardButton.WithCallbackData("🏠 Accueil", "iHome") }
                });

                await botClient.SendTextMessageAsync(chatId, "⚠️ <b>Paiement déjà en cours.</b> Veuillez régler votre facture ou l'annuler ci-dessous.", replyMarkup: cancelKeyboard, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                return "";
            }

            double priceValue = 0.0;
            if (update.CallbackQuery != null && !string.IsNullOrEmpty(update.CallbackQuery.Data))
            {
                string cbData = update.CallbackQuery.Data;
                if (cbData.Contains("_"))
                {
                    string parts = cbData.Split('_')[1];
                    double.TryParse(parts, out priceValue);
                }
                else
                {
                    double.TryParse(montant, out priceValue);
                }
            }
            else
            {
                double.TryParse(montant, out priceValue);
            }

            if (priceValue <= 0)
            {
                Console.WriteLine($"[Paiement Crypto Erreur] Montant invalide parsing: {priceValue} (brut: '{montant}')");
                await botClient.SendTextMessageAsync(chatId, "❌ Montant invalide. Veuillez réessayer.");
                return "";
            }

            Random rnd = new Random();
            int nombre = rnd.Next(10000, 100000);

            var jsonBody = new
            {
                amount = priceValue,
                currency = "EUR",
                lifetime = 30,
                fee_paid_by_payer = 1,
                under_paid_coverage = 2.5,
                auto_withdrawal = false,
                mixed_payment = true,
                return_url = "https://t.me/ChezRheyyBot",
                order_id = $"ORD-{nombre}",
                thanks_message = "Merci de votre achat sur ChezRheyyBot",
                description = $"Rechargement solde ChezRheyyBot #{nombre}",
                sandbox = false
            };

            var request = new HttpRequestMessage(HttpMethod.Post, config.apiUrl);
            request.Headers.Add("merchant_api_key", config.apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(jsonBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            Console.WriteLine($"[Paiement Crypto OxaPay] Reponse API status {(int)response.StatusCode}: {responseBody}");

            using JsonDocument doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data))
            {
                var trackId = data.GetProperty("track_id").GetString() ?? "";
                var paymentUrl = data.GetProperty("payment_url").GetString() ?? "";

                bool cree = DataBase.CreerPaiementEnBDD(chatId, trackId, priceValue, "CRYPTO", paymentUrl);
                Console.WriteLine($"[Paiement Crypto BDD] Facture enregistrée en BDD ({cree}): TrackID={trackId}, ChatID={chatId}, Montant={priceValue}€");

                try
                {
                    config.CustomPaiement.Remove(chatId);
                }
                catch { }

                foreach (var id in config.idAdmins)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(id, $"*Paiement Cryptomonnaie en cours*\n*User*: @{config.CurrentPseudo}\n*Montant*: {priceValue}€\n*Lien*:{paymentUrl}", parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                    }
                    catch { }
                }

                var paymentKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithUrl($"🔗 Payer ma commande ({priceValue:0.00} €)", paymentUrl) },
                    new[] { InlineKeyboardButton.WithCallbackData("❌ Annuler ma facture", "iCancelPaiement") },
                    new[] { InlineKeyboardButton.WithCallbackData("🏠 Accueil", "iHome") }
                });

                string msgText = $"<b>⚡ FACTURE DE PAIEMENT GENEREE</b>\n\n" +
                                 $"🆔 <b>Facture N°:</b> <code>{trackId}</code>\n" +
                                 $"💰 <b>Montant:</b> {priceValue:0.00} €\n" +
                                 $"⏱️ <b>Durée de validité:</b> 30 minutes\n\n" +
                                 $"<i>Cliquez sur le bouton ci-dessous pour effectuer votre paiement en crypto. Le solde sera crédité automatiquement à la validation.</i>";

                await botClient.SendTextMessageAsync(chatId, msgText, replyMarkup: paymentKeyboard, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
            }
            else
            {
                Console.WriteLine($"[Paiement Crypto Erreur] Impossible de créer la facture OxaPay pour ChatID {chatId}");
                await botClient.SendTextMessageAsync(config.idAdmin, $"Erreur: Impossible de créer facture pour l'ID: {chatId}");
                await botClient.SendTextMessageAsync(chatId, "❌ Erreur lors de la génération de la facture.");
                return "";
            }

            return "";
        }

        public static async Task VerifierPaiement(ITelegramBotClient botClient, CancellationToken cancellationToken)
        {
            Console.WriteLine("[Paiement Crypto Worker] Démarrage de la boucle de vérification OxaPay.");

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(3000, cancellationToken);

                var paiementsEnAttente = DataBase.ObtenirPaiementsEnAttenteBDD("CRYPTO");

                foreach (var item in paiementsEnAttente)
                {
                    try
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.oxapay.com/v1/payment/{item.TrackId}");
                        request.Headers.Add("merchant_api_key", config.apiKey);

                        var response = await _httpClient.SendAsync(request, cancellationToken);
                        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                        using JsonDocument doc = JsonDocument.Parse(responseBody);
                        var root = doc.RootElement;

                        if (!root.TryGetProperty("data", out var data))
                            continue;

                        string status = "";
                        if (data.TryGetProperty("status", out var statusProp))
                        {
                            status = statusProp.GetString() ?? "";
                        }

                        double montantReçu = item.Amount;
                        if (data.TryGetProperty("amount", out var amountProp))
                        {
                            if (amountProp.ValueKind == JsonValueKind.Number)
                            {
                                montantReçu = amountProp.GetDouble();
                            }
                            else if (amountProp.ValueKind == JsonValueKind.String)
                            {
                                double.TryParse(amountProp.GetString(), out montantReçu);
                            }
                        }

                        if (string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "overpaid", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"[Paiement Crypto OK] Facture {item.TrackId} validée ({status}), Montant: {montantReçu}€ pour ChatID: {item.ChatId}");
                            int result = config.UserSave.FindIndex(tuple => tuple.Item1 == long.Parse(item.ChatId));
                            if (result != -1)
                            {
                                var ancienTuple = config.UserSave[result];
                                double nouveauSolde = ancienTuple.Item3 + montantReçu;
                                config.UserSave[result] = Tuple.Create(ancienTuple.Item1, ancienTuple.Item2, nouveauSolde, ancienTuple.Item4);
                            }
                            else
                            {
                                config.UserSave.Add(Tuple.Create(long.Parse(item.ChatId), 0, montantReçu, false));
                            }

                            DataBase.SauvegarderUtilisateurs();

                            bool etaitExpire = string.Equals(item.Status, "EXPIRED", StringComparison.OrdinalIgnoreCase);

                            if (etaitExpire)
                            {
                                DataBase.MettreAJourPaiementStatutBDD(item.TrackId, "PAID");
                                Console.WriteLine($"[Paiement Tardif Reçu] Facture expirée {item.TrackId} payée tardivement ({montantReçu}€).");
                                try
                                {
                                    await botClient.SendTextMessageAsync(item.ChatId, $"✅ Votre paiement tardif de {montantReçu}€ a été détecté et crédité sur votre solde !");
                                    foreach (var idAdmin in config.idAdmins)
                                    {
                                        await botClient.SendTextMessageAsync(idAdmin, $"[PAIEMENT TARDIF] ChatID: {item.ChatId} | TrackID: {item.TrackId} | Montant: {montantReçu}€");
                                    }
                                }
                                catch { }
                            }
                            else
                            {
                                DataBase.MettreAJourPaiementStatutBDD(item.TrackId, "PAID");
                                Console.WriteLine($"[Paiement Crypto OK] Facture {item.TrackId} payée avec succès par ChatID: {item.ChatId}, Montant crédité: {montantReçu}€");
                                try
                                {
                                    await botClient.SendTextMessageAsync(item.ChatId, $"✅ Votre paiement de {montantReçu}€ a bien été validé et crédité !");
                                    foreach (var idAdmin in config.idAdmins)
                                    {
                                        await botClient.SendTextMessageAsync(idAdmin, $"[PAIEMENT REÇU] ChatID: {item.ChatId} | TrackID: {item.TrackId} | Montant: {montantReçu}€");
                                    }
                                }
                                catch { }
                            }
                        }
                        else if (status == "underpaid")
                        {
                            DataBase.MettreAJourPaiementStatutBDD(item.TrackId, "UNDERPAID");
                            Console.WriteLine($"[Paiement Partiell] Facture {item.TrackId} reçue partiellement ({montantReçu}€) pour ChatID: {item.ChatId}");

                            if (montantReçu > 0)
                            {
                                int result = config.UserSave.FindIndex(tuple => tuple.Item1 == long.Parse(item.ChatId));
                                if (result != -1)
                                {
                                    var ancienTuple = config.UserSave[result];
                                    double nouveauSolde = ancienTuple.Item3 + montantReçu;
                                    config.UserSave[result] = Tuple.Create(ancienTuple.Item1, ancienTuple.Item2, nouveauSolde, ancienTuple.Item4);
                                }
                                else
                                {
                                    config.UserSave.Add(Tuple.Create(long.Parse(item.ChatId), 0, montantReçu, false));
                                }

                                DataBase.SauvegarderUtilisateurs();
                            }

                            foreach (var id in config.idAdmins)
                            {
                                try
                                {
                                    await botClient.SendTextMessageAsync(id, $"⚠️ Paiement partiel (Underpaid) pour ID: {item.ChatId}\nCrédité: {montantReçu}€");
                                }
                                catch { }
                            }

                            try
                            {
                                await botClient.SendTextMessageAsync(long.Parse(item.ChatId), $"⚠️ Paiement partiel reçu. {montantReçu}€ ajoutés à votre solde.");
                            }
                            catch { }
                        }
                        else if (string.Equals(status, "expired", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.Equals(item.Status, "EXPIRED", StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine($"[Paiement Crypto Expiré] Facture {item.TrackId} expirée pour ChatID: {item.ChatId}");
                                DataBase.MettreAJourPaiementStatutBDD(item.TrackId, "EXPIRED");

                                try
                                {
                                    await botClient.SendTextMessageAsync(long.Parse(item.ChatId), $"Paiement <code>{item.TrackId}</code> expiré", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                                }
                                catch { }
                            }
                        }
                        else if (string.Equals(status, "canceled", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"[Paiement Crypto Annulé/Échec] Facture {item.TrackId} annulée pour ChatID: {item.ChatId}");
                            DataBase.MettreAJourPaiementStatutBDD(item.TrackId, "FAILED");

                            try
                            {
                                await botClient.SendTextMessageAsync(long.Parse(item.ChatId), $"Paiement <code>{item.TrackId}</code> annulé ou échoué", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erreur verif crypto pour {item.TrackId}: {ex.Message}");
                    }
                }
            }
        }

        private static string? _sumUpAccessToken = null;
        private static DateTime _sumUpTokenExpiration = DateTime.MinValue;

        private static async Task<string> ObtenirSumUpAccessToken(HttpClient client, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(_sumUpAccessToken) && DateTime.UtcNow < _sumUpTokenExpiration)
            {
                return _sumUpAccessToken;
            }

            string apiKey = Environment.GetEnvironmentVariable("SUMUP_API_KEY") ?? throw new InvalidOperationException("Variable d'environnement SUMUP_API_KEY manquante.");
            string clientId = Environment.GetEnvironmentVariable("SUMUP_CLIENT_ID") ?? throw new InvalidOperationException("Variable d'environnement SUMUP_CLIENT_ID manquante.");
            string clientSecret = Environment.GetEnvironmentVariable("SUMUP_CLIENT_SECRET") ?? throw new InvalidOperationException("Variable d'environnement SUMUP_CLIENT_SECRET manquante.");

            var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.sumup.com/token");
            var postData = $"grant_type=client_credentials&client_id={clientId}&client_secret={clientSecret}";
            tokenRequest.Content = new StringContent(postData, Encoding.UTF8, "application/x-www-form-urlencoded");
            tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var tokenResponse = await client.SendAsync(tokenRequest, cancellationToken);
            var tokenContent = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

            using var tokenJson = JsonDocument.Parse(tokenContent);
            _sumUpAccessToken = tokenJson.RootElement.GetProperty("access_token").GetString() ?? "";
            _sumUpTokenExpiration = DateTime.UtcNow.AddMinutes(50);

            return _sumUpAccessToken;
        }

        public static async Task CreerPaiementSumAPI(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken, int montant)
        {
            string chatId = config.CurrentChatId;
            if (string.IsNullOrEmpty(chatId)) return;

            Console.WriteLine($"[Paiement CB] Génération facture demandée par ChatID: {chatId}, Montant: {montant}€");

            try
            {
                if (DataBase.AUnPaiementEnAttenteBDD(chatId))
                {
                    var cancelKb = new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("❌ Annuler ma facture en attente", "iCancelPaiement") },
                        new[] { InlineKeyboardButton.WithCallbackData("🏠 Accueil", "iHome") }
                    });
                    await botClient.SendTextMessageAsync(chatId, "⚠️ <b>Vous avez déjà un paiement en attente.</b>", replyMarkup: cancelKb, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                    return;
                }

                DateTime expirationUtc = DateTime.UtcNow.AddMinutes(15);
                string formatted = expirationUtc.ToString("yyyy-MM-dd'T'HH:mm:sszzz");

                string proxyAddress = "50.117.12.56";
                int proxyPort = 50100;
                string proxyUser = "btcpaiement";
                string proxyPass = "iNDymRSU7L";

                var proxy = new WebProxy(proxyAddress, proxyPort)
                {
                    Credentials = new NetworkCredential(proxyUser, proxyPass)
                };

                var handler = new HttpClientHandler
                {
                    Proxy = proxy,
                    UseProxy = true,
                };

                using var client = new HttpClient(handler);

                Random rnd = new Random();
                int nombre = rnd.Next(10000, 100000);

                string domainEnv = Environment.GetEnvironmentVariable("RAILWAY_PUBLIC_DOMAIN") ?? Environment.GetEnvironmentVariable("RAILWAY_STATIC_URL") ?? "";
                string webhookUrl = !string.IsNullOrEmpty(domainEnv)
                    ? $"https://{domainEnv}/webhook/sumup/"
                    : "https://t.me/ChezRheyyBot";

                string payToEmail = Environment.GetEnvironmentVariable("SUMUP_PAY_TO_EMAIL") ?? throw new InvalidOperationException("Variable d'environnement SUMUP_PAY_TO_EMAIL manquante.");
                string accessToken = await ObtenirSumUpAccessToken(client, cancellationToken);

                var secondRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.sumup.com/v0.1/checkouts");
                var jsonPayload = new
                {
                    amount = montant,
                    checkout_reference = $"k8237fN914-6c0e-30f11-a5a52-{nombre}0285bggd",
                    currency = "EUR",
                    description = $"Rechargement solde ChezRheyyBot #{nombre}",
                    return_url = webhookUrl,
                    pay_to_email = payToEmail,
                    valid_until = formatted,
                    hosted_checkout = new { enabled = true }
                };

                secondRequest.Content = new StringContent(JsonSerializer.Serialize(jsonPayload), Encoding.UTF8, "application/json");
                secondRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var secondResponse = await client.SendAsync(secondRequest, cancellationToken);
                var secondContent = await secondResponse.Content.ReadAsStringAsync(cancellationToken);

                using var secondJson = JsonDocument.Parse(secondContent);
                string id = secondJson.RootElement.GetProperty("id").GetString() ?? "";
                string payementlink = secondJson.RootElement.GetProperty("hosted_checkout_url").GetString() ?? "";

                DataBase.CreerPaiementEnBDD(chatId, id, montant, "CB", payementlink);
                Console.WriteLine($"[Paiement CB BDD] Facture enregistrée en BDD: TrackID={id}, ChatID={chatId}, Montant={montant}€");

                foreach (var ids in config.idAdmins)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(ids, $"*Paiement via CB en cours*\nUser: @{config.CurrentPseudo}\nMontant: {montant}€\nLien: {payementlink}", parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                    }
                    catch { }
                }

                var cbKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithUrl($"💳 Payer par Carte Bancaire ({montant} €)", payementlink) },
                    new[] { InlineKeyboardButton.WithCallbackData("❌ Annuler ma facture", "iCancelPaiement") },
                    new[] { InlineKeyboardButton.WithCallbackData("🏠 Accueil", "iHome") }
                });

                await botClient.SendTextMessageAsync(chatId, $"<b>💳 FACTURE CARTE BANCAIRE GÉNÉRÉE</b>\n\nMontant : <b>{montant} €</b>\n\nCliquez ci-dessous pour procéder au paiement sécurisé :", replyMarkup: cbKeyboard, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur création CB pour {chatId}: {ex.Message}");
                await botClient.SendTextMessageAsync(chatId, "Une erreur est survenue lors de la création du paiement par carte.");
            }
        }

        public static async Task VerifierPaiementSumAPI(ITelegramBotClient botClient, CancellationToken cancellationToken)
        {
            Console.WriteLine("[Paiement CB Worker] Démarrage de la boucle de vérification SumUp.");

            try
            {
                var proxyAddress = "50.117.12.56";
                int proxyPort = 50100;
                var proxyUser = "btcpaiement";
                var proxyPass = "iNDymRSU7L";

                var proxy = new WebProxy(proxyAddress, proxyPort)
                {
                    Credentials = new NetworkCredential(proxyUser, proxyPass)
                };

                var handler = new HttpClientHandler
                {
                    Proxy = proxy,
                    UseProxy = true,
                };

                using var client = new HttpClient(handler);

                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(5000, cancellationToken);

                    var paiementsEnAttente = DataBase.ObtenirPaiementsEnAttenteBDD("CB");
                    if (paiementsEnAttente.Count == 0) continue;

                    string accessToken = await ObtenirSumUpAccessToken(client, cancellationToken);

                    foreach (var item in paiementsEnAttente)
                    {
                        try
                        {
                            var paiementverifier = new HttpRequestMessage(HttpMethod.Get, $"https://api.sumup.com/v0.1/checkouts/{item.TrackId}");
                            paiementverifier.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                            var tokenResponse2 = await client.SendAsync(paiementverifier, cancellationToken);
                            var tokenContent2 = await tokenResponse2.Content.ReadAsStringAsync(cancellationToken);

                            using var tokenJson2 = JsonDocument.Parse(tokenContent2);
                            var root = tokenJson2.RootElement;
                            string rsp2 = root.GetProperty("status").GetString() ?? "";

                            double montantSumUp = item.Amount;
                            if (root.TryGetProperty("amount", out var amtElem))
                            {
                                if (amtElem.ValueKind == JsonValueKind.Number)
                                    montantSumUp = amtElem.GetDouble();
                                else if (amtElem.ValueKind == JsonValueKind.String && double.TryParse(amtElem.GetString(), out double dblAmt))
                                    montantSumUp = dblAmt;
                            }

                            if (string.Equals(rsp2, "PAID", StringComparison.OrdinalIgnoreCase) || string.Equals(rsp2, "SUCCESSFUL", StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine($"[Paiement CB OK] Facture {item.TrackId} validée ({rsp2}), Montant: {montantSumUp}€ pour ChatID: {item.ChatId}");
                                DataBase.MettreAJourPaiementStatutBDD(item.TrackId, "PAID");

                                double nouveauSoldeTotal = 0;
                                int result = config.UserSave.FindIndex(tuple => tuple.Item1 == long.Parse(item.ChatId));
                                if (result != -1)
                                {
                                    var ancienTuple = config.UserSave[result];
                                    nouveauSoldeTotal = ancienTuple.Item3 + montantSumUp;
                                    config.UserSave[result] = Tuple.Create(ancienTuple.Item1, ancienTuple.Item2, nouveauSoldeTotal, ancienTuple.Item4);
                                }
                                else
                                {
                                    nouveauSoldeTotal = montantSumUp;
                                    config.UserSave.Add(Tuple.Create(long.Parse(item.ChatId), 0, montantSumUp, false));
                                }

                                DataBase.SauvegarderUtilisateurs();

                                foreach (var idAdmin in config.idAdmins)
                                {
                                    try
                                    {
                                        await botClient.SendTextMessageAsync(idAdmin, $"<b>[PAIEMENT CB REÇU]</b>\nUser ID: <code>{item.ChatId}</code>\nMontant: <b>{montantSumUp}€</b>", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                                    }
                                    catch { }
                                }

                                try
                                {
                                    var homeKb = new InlineKeyboardMarkup(new[]
                                    {
                                        new[] { InlineKeyboardButton.WithCallbackData("🏠 Retour au menu principal", "iHome") }
                                    });
                                    await botClient.SendTextMessageAsync(long.Parse(item.ChatId), $"✅ <b>PAIEMENT CARTE BANCAIRE REÇU</b>\n\nVotre compte a été crédité de <b>{montantSumUp} €</b> !\n💰 <b>Nouveau solde : {nouveauSoldeTotal} €</b>", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: homeKb);
                                }
                                catch { }
                            }
                            else if (string.Equals(rsp2, "FAILED", StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine($"[Paiement CB Échec] Facture {item.TrackId} refusée/échouée pour ChatID: {item.ChatId}");
                                DataBase.MettreAJourPaiementStatutBDD(item.TrackId, "FAILED");

                                try
                                {
                                    await botClient.SendTextMessageAsync(long.Parse(item.ChatId), "❌ <b>PAIEMENT ÉCHOUÉ</b>\n\nVotre transaction par Carte Bancaire a été refusée par la banque.", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                                }
                                catch { }
                            }
                            else if (string.Equals(rsp2, "CANCELLED", StringComparison.OrdinalIgnoreCase) || string.Equals(rsp2, "CANCELED", StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine($"[Paiement CB Annulé] Facture {item.TrackId} annulée pour ChatID: {item.ChatId}");
                                DataBase.MettreAJourPaiementStatutBDD(item.TrackId, "FAILED");

                                try
                                {
                                    await botClient.SendTextMessageAsync(long.Parse(item.ChatId), "❌ <b>FACTURE ANNULÉE</b>\n\nLa facture par Carte Bancaire a été annulée.", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                                }
                                catch { }
                            }
                            else if (string.Equals(rsp2, "EXPIRED", StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine($"[Paiement CB Expiré] Facture {item.TrackId} expirée pour ChatID: {item.ChatId}");
                                DataBase.MettreAJourPaiementStatutBDD(item.TrackId, "EXPIRED");

                                try
                                {
                                    var homeKb = new InlineKeyboardMarkup(new[]
                                    {
                                        new[] { InlineKeyboardButton.WithCallbackData("💳 Nouveau paiement", "iCustomP"), InlineKeyboardButton.WithCallbackData("🏠 Accueil", "iHome") }
                                    });
                                    await botClient.SendTextMessageAsync(long.Parse(item.ChatId), "⌛ <b>FACTURE EXPIRÉE</b>\n\nLe délai de 15 minutes pour effectuer le paiement par carte est dépassé.", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: homeKb);
                                }
                                catch { }

                                foreach (var idAdmin in config.idAdmins)
                                {
                                    try
                                    {
                                        await botClient.SendTextMessageAsync(idAdmin, $"[PAIEMENT CB EXPIRÉ] ChatID: {item.ChatId} | TrackID: {item.TrackId}");
                                    }
                                    catch { }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erreur verif SumUp pour {item.TrackId}: {ex.Message}");
                        }

                        await Task.Delay(500, cancellationToken);
                    }
                }
            }
            catch { }
        }

        private static readonly HashSet<string> _locksWebhook = new HashSet<string>();

        public static async Task LancerServeurWebhookSumUp(ITelegramBotClient botClient, CancellationToken cancellationToken)
        {
            int port = 8080;
            string? portEnv = Environment.GetEnvironmentVariable("PORT");
            if (!string.IsNullOrEmpty(portEnv) && int.TryParse(portEnv, out int p))
            {
                port = p;
            }

            HttpListener listener = new HttpListener();
            try
            {
                listener.Prefixes.Add($"http://*:{port}/webhook/sumup/");
                listener.Start();
                Console.WriteLine($"[Webhook SumUp] Serveur d'écoute HTTP démarré sur le port {port}.");
            }
            catch
            {
                try
                {
                    listener.Prefixes.Clear();
                    listener.Prefixes.Add($"http://+:{port}/webhook/sumup/");
                    listener.Start();
                    Console.WriteLine($"[Webhook SumUp] Serveur d'écoute HTTP démarré sur le port {port} (fallback +).");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Webhook SumUp Erreur Initialisation] {ex.Message}");
                    return;
                }
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    HttpListenerContext context = await listener.GetContextAsync();
                    _ = Task.Run(() => TraiterRequeteWebhook(context, botClient, cancellationToken), cancellationToken);
                }
                catch (Exception ex)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    Console.WriteLine($"[Webhook SumUp Listener Error] {ex.Message}");
                }
            }
        }

        private static async Task TraiterRequeteWebhook(HttpListenerContext context, ITelegramBotClient botClient, CancellationToken cancellationToken)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                if (request.HttpMethod == "POST" || request.HttpMethod == "GET")
                {
                    string checkoutId = "";

                    if (request.HttpMethod == "POST")
                    {
                        using var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding);
                        string body = await reader.ReadToEndAsync();
                        Console.WriteLine($"[Webhook SumUp Reçu POST] {body}");

                        using var doc = JsonDocument.Parse(body);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("checkout_id", out var cId)) checkoutId = cId.GetString() ?? "";
                        else if (root.TryGetProperty("resource_id", out var rId)) checkoutId = rId.GetString() ?? "";
                        else if (root.TryGetProperty("id", out var idElem)) checkoutId = idElem.GetString() ?? "";
                    }
                    else if (request.HttpMethod == "GET")
                    {
                        checkoutId = request.QueryString["checkout_id"] ?? request.QueryString["id"] ?? "";
                    }

                    if (!string.IsNullOrEmpty(checkoutId))
                    {
                        bool dejaEnCours = false;
                        lock (_locksWebhook)
                        {
                            if (_locksWebhook.Contains(checkoutId))
                            {
                                dejaEnCours = true;
                            }
                            else
                            {
                                _locksWebhook.Add(checkoutId);
                            }
                        }

                        if (!dejaEnCours)
                        {
                            try
                            {
                                var paiements = DataBase.ObtenirPaiementsEnAttenteBDD("CB");
                                var item = paiements.FirstOrDefault(p => p.TrackId == checkoutId);

                                if (item != null)
                                {
                                    using var client = new HttpClient();
                                    string accessToken = await ObtenirSumUpAccessToken(client, cancellationToken);

                                    var paiementverifier = new HttpRequestMessage(HttpMethod.Get, $"https://api.sumup.com/v0.1/checkouts/{checkoutId}");
                                    paiementverifier.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                                    var resp = await client.SendAsync(paiementverifier, cancellationToken);
                                    var respContent = await resp.Content.ReadAsStringAsync(cancellationToken);

                                    using var statusDoc = JsonDocument.Parse(respContent);
                                    var rootStatus = statusDoc.RootElement;
                                    string status = rootStatus.GetProperty("status").GetString() ?? "";

                                    double montantSumUp = item.Amount;
                                    if (rootStatus.TryGetProperty("amount", out var amtElem))
                                    {
                                        if (amtElem.ValueKind == JsonValueKind.Number) montantSumUp = amtElem.GetDouble();
                                        else if (amtElem.ValueKind == JsonValueKind.String && double.TryParse(amtElem.GetString(), out double dblAmt)) montantSumUp = dblAmt;
                                    }

                                    if (string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "SUCCESSFUL", StringComparison.OrdinalIgnoreCase))
                                    {
                                        Console.WriteLine($"[Webhook SumUp VALIDÉ] Facture {checkoutId} payée avec succès !");
                                        DataBase.MettreAJourPaiementStatutBDD(checkoutId, "PAID");

                                        double nouveauSoldeTotal = 0;
                                        int result = config.UserSave.FindIndex(tuple => tuple.Item1 == long.Parse(item.ChatId));
                                        if (result != -1)
                                        {
                                            var ancienTuple = config.UserSave[result];
                                            nouveauSoldeTotal = ancienTuple.Item3 + montantSumUp;
                                            config.UserSave[result] = Tuple.Create(ancienTuple.Item1, ancienTuple.Item2, nouveauSoldeTotal, ancienTuple.Item4);
                                        }
                                        else
                                        {
                                            nouveauSoldeTotal = montantSumUp;
                                            config.UserSave.Add(Tuple.Create(long.Parse(item.ChatId), 0, montantSumUp, false));
                                        }

                                        DataBase.SauvegarderUtilisateurs();

                                        foreach (var idAdmin in config.idAdmins)
                                        {
                                            try
                                            {
                                                await botClient.SendTextMessageAsync(idAdmin, $"<b>[WEBHOOK SUMUP REÇU]</b>\nUser ID: <code>{item.ChatId}</code>\nMontant: <b>{montantSumUp}€</b>", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                                            }
                                            catch { }
                                        }

                                        try
                                        {
                                            var homeKb = new InlineKeyboardMarkup(new[]
                                            {
                                                new[] { InlineKeyboardButton.WithCallbackData("🏠 Retour au menu principal", "iHome") }
                                            });
                                            await botClient.SendTextMessageAsync(long.Parse(item.ChatId), $"✅ <b>PAIEMENT CARTE BANCAIRE REÇU (WEBHOOK)</b>\n\nVotre compte a été crédité de <b>{montantSumUp} €</b> !\n💰 <b>Nouveau solde : {nouveauSoldeTotal} €</b>", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: homeKb);
                                        }
                                        catch { }
                                    }
                                }
                            }
                            finally
                            {
                                lock (_locksWebhook)
                                {
                                    _locksWebhook.Remove(checkoutId);
                                }
                            }
                        }
                    }

                    byte[] responseBuffer = Encoding.UTF8.GetBytes("{\"success\":true}");
                    response.ContentType = "application/json";
                    response.ContentLength64 = responseBuffer.Length;
                    response.StatusCode = 200;
                    await response.OutputStream.WriteAsync(responseBuffer, 0, responseBuffer.Length);
                    response.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Webhook SumUp Traitement Erreur] {ex.Message}");
            }
        }
    }
}
