using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace UgcBotTG
{
    internal class paiement
    {
        public static async Task PaiementList(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("💳 Paiement CarteBancaire", "iCustomCB")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("₿ Paiement Cryptomonnaie", "iCustomP"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Home", "iHome")
                }
            });

            try
            {
                await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]) - 1);
                await botClient.SendTextMessageAsync(config.CurrentChatId, "Rechargement *AUTOMATIQUE* via CryptoMonnaie|CarteBancaire\n\n", replyMarkup: inlineKeyboard, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
            }
            catch
            {
                try
                {
                    await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]));
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Rechargement *AUTOMATIQUE* via CryptoMonnaie|CarteBancaire\n\n", replyMarkup: inlineKeyboard, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                }
                catch
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Rechargement *AUTOMATIQUE* via CryptoMonnaie|CarteBancaire\n\n", replyMarkup: inlineKeyboard, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                }
            }
        }

        public static async Task<string> GenerateLink(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken, string montant = "")
        {
            string chatId = config.CurrentChatId;
            if (string.IsNullOrEmpty(chatId)) return "";

            if (DataBase.AUnPaiementEnAttenteBDD(chatId))
            {
                await botClient.SendTextMessageAsync(chatId, "⚠️ Tu as déjà créé un lien de paiement en attente. Veuillez payer avec ou attendre son expiration.");
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
                await botClient.SendTextMessageAsync(chatId, "❌ Montant invalide.");
                return "";
            }

            Random rnd = new Random();
            int nombre = rnd.Next(10000, 100000);

            var jsonBody = new
            {
                amount = priceValue,
                currency = "EUR",
                lifetime = 40,
                fee_paid_by_payer = 1,
                under_paid_coverage = 2.5,
                to_currency = "USDT",
                auto_withdrawal = false,
                mixed_payment = true,
                return_url = "https://example.com/success",
                email = "customer@oxapay.com",
                order_id = $"ORD-{nombre}",
                thanks_message = "Merci de votre achat",
                description = $"Achat #{nombre}",
                sandbox = false
            };

            using var httpClient = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, config.apiUrl);
            request.Headers.Add("merchant_api_key", config.apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(jsonBody), Encoding.UTF8, "application/json");

            var response = await httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            using JsonDocument doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data))
            {
                var trackId = data.GetProperty("track_id").GetString() ?? "";
                var paymentUrl = data.GetProperty("payment_url").GetString() ?? "";

                DataBase.CreerPaiementEnBDD(chatId, trackId, priceValue, "CRYPTO", paymentUrl);

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

                await botClient.SendTextMessageAsync(chatId, $"N° facture: <code>{trackId}</code>\nPaiement: {paymentUrl}\n", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
            }
            else
            {
                await botClient.SendTextMessageAsync(config.idAdmin, $"Erreur: Impossible de créer facture pour l'ID: {chatId}");
                await botClient.SendTextMessageAsync(chatId, "❌ Erreur lors de la génération de la facture.");
                return "";
            }

            return "";
        }

        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task VerifierPaiement(ITelegramBotClient botClient, CancellationToken cancellationToken)
        {
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

                        if (string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase))
                        {
                            DataBase.MettreAJourPaiementStatutBDD(item.TrackId, "PAID");

                            int result = config.UserSave.FindIndex(tuple => tuple.Item1 == long.Parse(item.ChatId));
                            if (result != -1)
                            {
                                var ancienTuple = config.UserSave[result];
                                double nouveauSolde = ancienTuple.Item3 + montantReçu;
                                config.UserSave[result] = Tuple.Create(ancienTuple.Item1, ancienTuple.Item2, nouveauSolde);
                            }
                            else
                            {
                                config.UserSave.Add(Tuple.Create(long.Parse(item.ChatId), 0, montantReçu));
                            }

                            DataBase.SauvegarderUtilisateurs();

                            foreach (var id in config.idAdmins)
                            {
                                try
                                {
                                    await botClient.SendTextMessageAsync(id, $"[+] Solde ajouté à ID: {item.ChatId}\nCrypto: {montantReçu}€");
                                }
                                catch { }
                            }

                            try
                            {
                                await botClient.SendTextMessageAsync(long.Parse(item.ChatId), $"💰 {montantReçu}€ reçus sur votre solde.");
                            }
                            catch { }
                        }
                        else if (string.Equals(status, "expired", StringComparison.OrdinalIgnoreCase))
                        {
                            DataBase.MettreAJourPaiementStatutBDD(item.TrackId, "EXPIRED");

                            try
                            {
                                await botClient.SendTextMessageAsync(long.Parse(item.ChatId), $"Paiement <code>{item.TrackId}</code> expiré", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
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

        public static async Task RecupererMontant(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if (!config.CustomPaiement.Contains(config.CurrentChatId))
                {
                    config.CustomPaiement.Add(config.CurrentChatId);
                }
                await botClient.SendTextMessageAsync(config.CurrentChatId, "Indiquez le montant souhaité: ");
            }
            catch { }
        }

        public static async Task CreerPaiementSumAPI(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken, int montant)
        {
            string chatId = config.CurrentChatId;
            if (string.IsNullOrEmpty(chatId)) return;

            try
            {
                if (DataBase.AUnPaiementEnAttenteBDD(chatId))
                {
                    await botClient.SendTextMessageAsync(chatId, "⚠️ Vous avez déjà un paiement en attente. Veuillez finaliser ou attendre son expiration.");
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

                var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.sumup.com/token");
                var postData = "grant_type=client_credentials&client_id=cc_classic_PFUMM8wpZdtpvxx1I71gMv2a6PbQQ&client_secret=cc_sk_classic_JIDwonLeeMtiT7csQ4uvCIhU42dTvd0qOFPHWFqZAZwjkxpYRF";
                tokenRequest.Content = new StringContent(postData, Encoding.UTF8, "application/x-www-form-urlencoded");
                tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "sup_sk_gn5RFRxslgQZ2Ca010BIry33E9l56yQ5f");

                var tokenResponse = await client.SendAsync(tokenRequest, cancellationToken);
                var tokenContent = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

                using var tokenJson = JsonDocument.Parse(tokenContent);
                string accessToken = tokenJson.RootElement.GetProperty("access_token").GetString() ?? "";

                var secondRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.sumup.com/v0.1/checkouts");
                var jsonPayload = new
                {
                    amount = montant,
                    checkout_reference = $"k8237fN914-6c0e-30f11-a5a52-{nombre}0285bggd",
                    currency = "EUR",
                    description = "Paiement de produit",
                    merchant_code = "M5QBRGXB",
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

                foreach (var ids in config.idAdmins)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(ids, $"**Paiement via CB en cours**\nUser: @{config.CurrentPseudo}\nMontant: {montant}€\nLien: {payementlink}", parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                    }
                    catch { }
                }

                await botClient.SendTextMessageAsync(chatId, $"Voici votre lien de paiement {montant}€: {payementlink}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur création CB pour {chatId}: {ex.Message}");
                await botClient.SendTextMessageAsync(chatId, "Une erreur est survenue lors de la création du paiement par carte.");
            }
        }

        public static async Task VerifierPaiementSumAPI(ITelegramBotClient botClient, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(5000, cancellationToken);

                    var paiementsEnAttente = DataBase.ObtenirPaiementsEnAttenteBDD("CB");

                    foreach (var item in paiementsEnAttente)
                    {
                        try
                        {
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

                            var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.sumup.com/token");
                            var postData = "grant_type=client_credentials&client_id=cc_classic_PFUMM8wpZdtpvxx1I71gMv2a6PbQQ&client_secret=cc_sk_classic_JIDwonLeeMtiT7csQ4uvCIhU42dTvd0qOFPHWFqZAZwjkxpYRF";
                            tokenRequest.Content = new StringContent(postData, Encoding.UTF8, "application/x-www-form-urlencoded");
                            tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "sup_sk_gn5RFRxslgQZ2Ca010BIry33E9l56yQ5f");

                            var tokenResponse = await client.SendAsync(tokenRequest, cancellationToken);
                            var tokenContent = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

                            using var tokenJson = JsonDocument.Parse(tokenContent);
                            string accessToken = tokenJson.RootElement.GetProperty("access_token").GetString() ?? "";

                            var paiementverifier = new HttpRequestMessage(HttpMethod.Get, $"https://api.sumup.com/v0.1/checkouts/{item.TrackId}");
                            paiementverifier.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                            var tokenResponse2 = await client.SendAsync(paiementverifier, cancellationToken);
                            var tokenContent2 = await tokenResponse2.Content.ReadAsStringAsync(cancellationToken);

                            using var tokenJson2 = JsonDocument.Parse(tokenContent2);
                            string rsp2 = tokenJson2.RootElement.GetProperty("status").GetString() ?? "";

                            if (rsp2 == "PAID")
                            {
                                DataBase.MettreAJourPaiementStatutBDD(item.TrackId, "PAID");

                                int result = config.UserSave.FindIndex(tuple => tuple.Item1 == long.Parse(item.ChatId));
                                if (result != -1)
                                {
                                    var ancienTuple = config.UserSave[result];
                                    double nouveauSolde = ancienTuple.Item3 + item.Amount;
                                    config.UserSave[result] = Tuple.Create(ancienTuple.Item1, ancienTuple.Item2, nouveauSolde);
                                }
                                else
                                {
                                    config.UserSave.Add(Tuple.Create(long.Parse(item.ChatId), 0, item.Amount));
                                }

                                DataBase.SauvegarderUtilisateurs();

                                foreach (var idAdmin in config.idAdmins)
                                {
                                    try
                                    {
                                        await botClient.SendTextMessageAsync(idAdmin, $"**Paiement finalisé par CB**\nID: {item.ChatId}\nMontant: {item.Amount}€", parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                                    }
                                    catch { }
                                }
                                await botClient.SendTextMessageAsync(long.Parse(item.ChatId), $"Paiement reçu de votre part {item.Amount}€");
                            }
                            else if (rsp2 == "EXPIRED")
                            {
                                DataBase.MettreAJourPaiementStatutBDD(item.TrackId, "EXPIRED");

                                try
                                {
                                    await botClient.SendTextMessageAsync(long.Parse(item.ChatId), $"Paiement expiré.");
                                }
                                catch { }

                                foreach (var idAdmin in config.idAdmins)
                                {
                                    try
                                    {
                                        await botClient.SendTextMessageAsync(idAdmin, $"**Paiement expiré**\nId :{item.ChatId}", parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
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
    }
}
