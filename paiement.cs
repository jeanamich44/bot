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
                await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]));

                await botClient.SendTextMessageAsync(config.CurrentChatId, "Rechargement *AUTOMATIQUE* via CryptoMonnaie|CarteBancaire\n\n", replyMarkup: inlineKeyboard, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
            }
        }

        public static async Task<string> GenerateLink(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken, string montant = "")
        {
            if (config.PayementLink.ContainsKey(config.CurrentChatId))
            {
                await botClient.SendTextMessageAsync(config.CurrentChatId, $"⚠️ Tu a deja créé un lien de paiement. Veuillez payer avec ou attendre l'expiration pour créer un nouveau lien. ");
                return "";
            }

            Random rnd = new Random();
            int nombre = rnd.Next(10000, 100000);

            var price = "";

            if(update.CallbackQuery == null)
            {
                price = montant;
            }
            else
            {
                price = update.CallbackQuery.Data.Split('_')[1].ToString();
                if (price.Length != 2)
                {
                    return "";
                }
            }

            var jsonBody = new
            {
                amount = price,
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

            var httpClient = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, config.apiUrl);
            request.Headers.Add("merchant_api_key", config.apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(jsonBody), Encoding.UTF8, "application/json");

            var response = await httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            using JsonDocument doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data))
            {
                var trackId = data.GetProperty("track_id").GetString();
                var paymentUrl = data.GetProperty("payment_url").GetString();

                config.PayementLink.Add(config.CurrentChatId, trackId);
                config.IdPaiement.Add(config.CurrentChatId);

                try
                {
                    config.CustomPaiement.Remove(config.CurrentChatId);
                }
                catch { }


                foreach (var id in config.idAdmins)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(id, $"*Paiement Crytomonnaie en cours*\n*User*: @{config.CurrentPseudo}\n*Montant*: {price}€\n*TrackId*:{paymentUrl}",parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);

                    }
                    catch
                    {

                    }
                }
                await botClient.SendTextMessageAsync(config.CurrentChatId, $"N° facture: <code>{trackId}</code>\nPaiement: {paymentUrl}\n", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
            }
            else
            {
                await botClient.SendTextMessageAsync(config.idAdmin, $"Erreur: Impossible de creer facture id: {config.CurrentChatId}\n");
                return "";
            }

            return "";
        }

        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task VerifierPaiement(ITelegramBotClient botClient, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(2000, cancellationToken);

                // on prend une "photo" des paiements en cours pour éviter les problèmes de suppression dans la boucle
                var paiements = config.IdPaiement.Distinct().ToList();

                foreach (var paiementId in paiements)
                {
                    try
                    {
                        if (!config.PayementLink.TryGetValue(paiementId, out var trackid))
                            continue; // si pas trouvé -> skip

                        var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.oxapay.com/v1/payment/{trackid}");
                        request.Headers.Add("merchant_api_key", config.apiKey);

                        var response = await _httpClient.SendAsync(request, cancellationToken);
                        var responseBody = await response.Content.ReadAsStringAsync();

                        using JsonDocument doc = JsonDocument.Parse(responseBody);
                        var root = doc.RootElement;

                        if (!root.TryGetProperty("data", out var data))
                            continue;

                        var status = data.GetProperty("status").GetString();
                        Console.WriteLine(status);
                        var montant = data.GetProperty("amount").GetDouble();

                        if (string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase))
                        {
                            int result = config.UserSave.FindIndex(tuple => tuple.Item1 == long.Parse(paiementId));

                            if (result != -1)
                            {
                                var ancienTuple = config.UserSave[result];
                                double nouveauSolde = ancienTuple.Item3 + montant;
                                config.UserSave[result] = Tuple.Create(ancienTuple.Item1, ancienTuple.Item2, nouveauSolde);
                            }
                            else
                            {
                                config.UserSave.Add(Tuple.Create(long.Parse(paiementId), 0, montant));
                            }

                            // suppression après traitement (fait avant notifications pour éviter la boucle infinie si Telegram échoue)
                            config.PayementLink.Remove(paiementId);
                            config.IdPaiement.RemoveAll(x => x == paiementId);

                            try
                            {
                                foreach (var id in config.idAdmins)
                                {
                                    try
                                    {
                                        await botClient.SendTextMessageAsync(id, $"[+] Solde ajouté à ID: {paiementId}\nCrypto: {montant}€");
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Erreur notification admin {id} : {ex.Message}");
                                    }
                                }
                                await botClient.SendTextMessageAsync(long.Parse(paiementId), $"💰 {montant}€ reçus sur votre solde.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Erreur notification utilisateur {paiementId} : {ex.Message}");
                            }
                        }
                        else if (string.Equals(status, "expired", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                await botClient.SendTextMessageAsync(long.Parse(paiementId), $"Paiement <code>{trackid}</code> expiré", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                            }
                            catch { }

                            config.PayementLink.Remove(paiementId);
                            config.IdPaiement.RemoveAll(x => x == paiementId);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erreur pour {paiementId}: {ex.Message}");
                    }
                }
            }
        }



        //gere les paiement custom

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

            catch
            {

            }
        }

        //paiement carte bancaire

        public static async Task CreerPaiementSumAPI(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken,int montant)
        {
            try
            {
                DateTime expirationUtc = DateTime.UtcNow.AddMinutes(15);

                // Fuseau horaire de Paris (fonctionne sous Windows)
                TimeZoneInfo parisTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");

                // Convertir en heure de Paris
                DateTime expirationParis = TimeZoneInfo.ConvertTimeFromUtc(expirationUtc, parisTimeZone);

                // Formater en ISO 8601 avec le bon offset horaire (+01:00 ou +02:00)
                string formatted = expirationParis.ToString("yyyy-MM-dd'T'HH:mm:sszzz");
                //
                //DateTime expirationUtc = DateTime.UtcNow.AddMinutes(15);

                // string formatted = expirationUtc.ToString("yyyy-MM-dd'T'HH:mm:ss+00:00");

                string proxyAddress = "50.117.12.56";   // Remplace par ton IP
                int proxyPort = 50100;                  // Remplace par le port
                string proxyUser = "btcpaiement";       // Remplace par le user
                string proxyPass = "iNDymRSU7L";       // Remplace par le mot de passe

                var proxy = new WebProxy(proxyAddress, proxyPort)
                {
                    Credentials = new NetworkCredential(proxyUser, proxyPass)
                };

                var handler = new HttpClientHandler
                {
                    Proxy = proxy,
                    UseProxy = true,
                };

                // Remplacer l'ancien HttpClient par celui-ci
                using var client = new HttpClient(handler);


                if (config.PayementAPI.ContainsKey(config.CurrentChatId))
                {
                    config.PayementAPI.Remove(config.CurrentChatId);


                    try
                    {
                        config.MontantPayement.Remove(config.CurrentChatId);
                        config.AttentePaiement.Remove(config.CurrentChatId);
                    }
                    catch
                    {

                    }
                    // var link = config.PayementAPI[config.CurrentChatId];
                    //await botClient.SendTextMessageAsync(config.CurrentChatId, $"Un Liens de paiement es deja en cours pour vous. {link}\n");
                    //return;
                }
                
                

                Random rnd = new Random();
                int nombre = rnd.Next(10000, 100000);

                var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.sumup.com/token");

                var postData = "grant_type=client_credentials&client_id=cc_classic_PFUMM8wpZdtpvxx1I71gMv2a6PbQQ&client_secret=cc_sk_classic_JIDwonLeeMtiT7csQ4uvCIhU42dTvd0qOFPHWFqZAZwjkxpYRF";
                tokenRequest.Content = new StringContent(postData, Encoding.UTF8, "application/x-www-form-urlencoded");

                tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "sup_sk_gn5RFRxslgQZ2Ca010BIry33E9l56yQ5f");

                var tokenResponse = await client.SendAsync(tokenRequest);
                var tokenContent = await tokenResponse.Content.ReadAsStringAsync();

               // Console.WriteLine(tokenContent);

                using var tokenJson = JsonDocument.Parse(tokenContent);
                string accessToken = tokenJson.RootElement.GetProperty("access_token").GetString();

               // Console.WriteLine(accessToken);

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

                var jsonString = JsonSerializer.Serialize(jsonPayload);
                secondRequest.Content = new StringContent(jsonString, Encoding.UTF8, "application/json");
                secondRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var secondResponse = await client.SendAsync(secondRequest);
                var secondContent = await secondResponse.Content.ReadAsStringAsync();

               // Console.WriteLine(secondContent);

                using var secondJson = JsonDocument.Parse(secondContent);
                 try
                 {
                     string id = secondJson.RootElement.GetProperty("id").GetString();
                     string payementlink = secondJson.RootElement.GetProperty("hosted_checkout_url").GetString();
                     string idAdmin = secondJson.RootElement.GetProperty("checkout_reference").GetString();
 
                     foreach (var ids in config.idAdmins)
                     {
                         try
                         {
                             await botClient.SendTextMessageAsync(ids, $"**Paiement via CB en cours**\nUser: @{config.CurrentPseudo}\nMontant: {montant}€\nPayementLink: {payementlink}", parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                         }
                         catch
                         {
                             // await botClient.SendTextMessageAsync(ids, $"**Paiement via CB en cours**\nID: @{config.CurrentChatId}\nMontant: {montant}€\nPayementLink: {payementlink}", parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                         }
                     }
 
                     config.PayementAPI.Add(config.CurrentChatId, id); // On stocke l'ID directement au lieu du lien de paiement
 
                     await botClient.SendTextMessageAsync(config.CurrentChatId, $"Voici votre liens de paiement {montant}€: {payementlink}");
                     config.MontantPayement.Add(config.CurrentChatId, montant.ToString());
                     // Plus de démarrage de boucle VerifierPaiementSumAPI ici, c'est maintenant global.
                 }
                 catch
                 {
                     await botClient.SendTextMessageAsync(config.CurrentChatId, "Une erreur es survenue.");
                     return;
                 }
             }
             catch
             {
 
             }
         }
 
         private static readonly object _lockPayementAPI = new();
 
         public static async Task VerifierPaiementSumAPI(ITelegramBotClient botClient, CancellationToken cancellationToken)
         {
             try
             {
                 while (!cancellationToken.IsCancellationRequested)
                 {
                     List<KeyValuePair<string, string>> items;
 
                     lock (_lockPayementAPI)
                     {
                         items = config.PayementAPI.ToList();
                     }
 
                     var processedKeys = new HashSet<string>();
 
                     foreach (var (key, value) in items)
                     {
                         if (processedKeys.Contains(key))
                             continue;
 
                         if (value == null)
                             continue;
 
                         try
                         {
                             string proxyAddress = "50.117.12.56";   // Remplace par ton IP
                             int proxyPort = 50100;                  // Remplace par le port
                             string proxyUser = "btcpaiement";       // Remplace par le user
                             string proxyPass = "iNDymRSU7L";       // Rempl
 
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
 
                             var id = value; // Puisqu'on stocke l'ID directement
 
                             var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.sumup.com/token");
 
                             var postData = "grant_type=client_credentials&client_id=cc_classic_PFUMM8wpZdtpvxx1I71gMv2a6PbQQ&client_secret=cc_sk_classic_JIDwonLeeMtiT7csQ4uvCIhU42dTvd0qOFPHWFqZAZwjkxpYRF";
                             tokenRequest.Content = new StringContent(postData, Encoding.UTF8, "application/x-www-form-urlencoded");
                             tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "sup_sk_gn5RFRxslgQZ2Ca010BIry33E9l56yQ5f");
 
                             var tokenResponse = await client.SendAsync(tokenRequest);
                             var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
 
                             using var tokenJson = JsonDocument.Parse(tokenContent);
                             string accessToken = tokenJson.RootElement.GetProperty("access_token").GetString();
 
                             var paiementverifier = new HttpRequestMessage(HttpMethod.Get, $"https://api.sumup.com/v0.1/checkouts/{id}");
                             paiementverifier.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
 
                             var tokenResponse2 = await client.SendAsync(paiementverifier);
                             var tokenContent2 = await tokenResponse2.Content.ReadAsStringAsync();
 
                             using var tokenJson2 = JsonDocument.Parse(tokenContent2);
                             string rsp2 = tokenJson2.RootElement.GetProperty("status").GetString();
 
                             if (rsp2 == "PAID")
                             {
                                 string solde;
                                 lock (_lockPayementAPI)
                                 {
                                     solde = config.MontantPayement[key];
 
                                     config.PayementAPI.Remove(key);
                                     config.MontantPayement.Remove(key);
                                     config.AttentePaiement.Remove(key);
                                 }
 
                                 // Créditer le solde d'abord
                                 int result = config.UserSave.FindIndex(tuple => tuple.Item1 == long.Parse(key));
                                 if (result != -1)
                                 {
                                     var ancienTuple = config.UserSave[result];
                                     double nouveauSolde = ancienTuple.Item3 + double.Parse(solde);
                                     config.UserSave[result] = Tuple.Create(ancienTuple.Item1, ancienTuple.Item2, nouveauSolde);
                                 }
                                 else
                                 {
                                     config.UserSave.Add(Tuple.Create(long.Parse(key), 0, double.Parse(solde)));
                                 }
 
                                 // Envoyer les notifications de façon sécurisée
                                 try
                                 {
                                     foreach (var idAdmin in config.idAdmins)
                                     {
                                         try
                                         {
                                             await botClient.SendTextMessageAsync(idAdmin, $"**Paiement finalisé par CB**\nID: {key}\n", parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                                         }
                                         catch (Exception ex)
                                         {
                                             Console.WriteLine($"Erreur notification admin {idAdmin} SumUp: {ex.Message}");
                                         }
                                     }
                                     await botClient.SendTextMessageAsync(long.Parse(key), $"Paiement reçu de votre part {solde}€");
                                 }
                                 catch (Exception ex)
                                 {
                                     Console.WriteLine($"Erreur notification utilisateur {key} SumUp: {ex.Message}");
                                 }
 
                                 processedKeys.Add(key);
                             }
                             else if (rsp2 == "EXPIRED")
                             {
                                 lock (_lockPayementAPI)
                                 {
                                     config.banAPI.Add(key);
                                     config.PayementAPI.Remove(key);
                                     config.MontantPayement.Remove(key);
                                     config.AttentePaiement.Remove(key);
                                 }
 
                                 try
                                 {
                                     await botClient.SendTextMessageAsync(long.Parse(key), $"Paiement expiré. Vous ne pouvez plus créer de liens pendant 20min.");
                                 }
                                 catch { }
 
                                 try
                                 {
                                     foreach (var idAdmin in config.idAdmins)
                                     {
                                         try
                                         {
                                             await botClient.SendTextMessageAsync(idAdmin, $"**Paiement expiré**\nId :{key}", parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                                         }
                                         catch { }
                                     }
                                 }
                                 catch { }
 
                                 processedKeys.Add(key);
                             }
                             else if (rsp2 == "PENDING")
                             {
                                 // Pas encore payé, on passe
                             }
                             else
                             {
                                 // Statut inattendu, ignore ou log
                             }
                         }
                         catch (Exception ex)
                         {
                             // Log l'erreur pour debug
                             Console.WriteLine($"Erreur dans VerifierPaiementSumAPI pour {key} : {ex.Message}");
                         }
 
                         // On peut mettre un petit delay ici si besoin (ex: 500ms)
                         await Task.Delay(500, cancellationToken);
                     }
 
                     // Attente avant la prochaine boucle complète (ex: 5s)
                     await Task.Delay(5000, cancellationToken);
                 }
             }
             catch
             {
 
             }
         }
    }
}
