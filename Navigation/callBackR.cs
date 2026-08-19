using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ChezRheyyBot
{
    internal class callBackR
    {
        public static async Task ResponseCallBack(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if (update.CallbackQuery?.Data == null)
                {
                    return;
                }

                string data = update.CallbackQuery.Data;
                string chatId = config.CurrentChatId;

                if (data == "iCarrefour")
                {
                    await SendCarrefourStock(botClient, update, cancellationToken, chatId);
                    return;
                }
                if (data == "iIPTV")
                {
                    await SendIptvStock(botClient, update, cancellationToken, chatId);
                    return;
                }
                if (data.Contains("iiptv"))
                {
                    await AcheterIptv(botClient, update, cancellationToken, chatId, data);
                    return;
                }
                if (data == "iHome")
                {
                    await SampleM.SendMessage(botClient, update, cancellationToken);
                    return;
                }
                if (data == "iPaiement")
                {
                    await paiement.PaiementList(botClient, update, cancellationToken);
                    return;
                }
                if (data.Contains("stock_"))
                {
                    await achat.DemandeAchat("", botClient, update, cancellationToken);
                    return;
                }
                if (data.Contains("iCustomP"))
                {
                    if (config.EstEnCooldownPaiement(chatId))
                    {
                        await botClient.SendTextMessageAsync(chatId, "Il ne sera plus possible de créer des liens pendant 24 heures. Vous serez informé dès que vous pourrez en créer un.");
                        return;
                    }

                    config.CustomPaiement.TryAdd(chatId, 0);
                    await botClient.SendTextMessageAsync(chatId, "Merci de rentrer un montant à recharger");
                    return;
                }
                if (data.StartsWith("iPayAmt_"))
                {
                    string montant = data.Replace("iPayAmt_", "");
                    await paiement.GenerateLink(botClient, update, cancellationToken, montant);
                    return;
                }
                if (data.Contains("iMontantPersoCrypto"))
                {
                    await paiement.ActiverSaisieCustom(botClient, update, cancellationToken);
                    return;
                }
                if (data.Contains("iCancelPaiement"))
                {
                    await paiement.AnnulerPaiement(botClient, update, cancellationToken);
                    return;
                }
                if (data.Contains("iCustomCB"))
                {
                    if (config.EstEnCooldownPaiement(chatId))
                    {
                        await botClient.SendTextMessageAsync(chatId, "Il ne sera plus possible de créer des liens pendant 24 heures. Vous serez informé dès que vous pourrez en créer un.");
                        return;
                    }

                    if (config.AttentePaiement.ContainsKey(chatId))
                    {
                        await botClient.SendTextMessageAsync(chatId, "Merci de rentrer un montant à recharger");
                        return;
                    }

                    config.AttentePaiement.TryAdd(chatId, 0);
                    await botClient.SendTextMessageAsync(chatId, "Merci de rentrer un montant à recharger");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Callback Erreur] {ex.Message}");
            }
        }

        private static async Task AcheterIptv(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken, string chatId, string msg)
        {
            double.TryParse(config.GetSetting("iptv", "price_1m", "5"), out double p1);
            double.TryParse(config.GetSetting("iptv", "price_3m", "10"), out double p3);
            double.TryParse(config.GetSetting("iptv", "price_6m", "15"), out double p6);
            double.TryParse(config.GetSetting("iptv", "price_12m", "30"), out double p12);

            Dictionary<int, double> prixParMois = new()
            {
                { 1, p1 },
                { 3, p3 },
                { 6, p6 },
                { 12, p12 }
            };

            if (!int.TryParse(new string(msg.Where(char.IsDigit).ToArray()), out int number) || !long.TryParse(chatId, out long userId))
            {
                await botClient.SendTextMessageAsync(chatId, "Offre invalide.");
                return;
            }

            var user = config.TrouverUtilisateur(userId);
            if (user == null)
            {
                return;
            }

            double solde = user.Solde;
            if (!prixParMois.TryGetValue(number, out double prix))
            {
                await botClient.SendTextMessageAsync(chatId, "Abonnement non disponible.");
                return;
            }

            if (solde < prix)
            {
                await botClient.SendTextMessageAsync(chatId, $"Solde insuffisant ❌\nPrix : {prix}€\nVotre solde : {solde}€");
                return;
            }

            Console.WriteLine($"[IPTV BUY] User: {chatId} | Formule: {number} mois | Prix: {prix}€ | Solde actuel: {solde}€");
            string link = await iptv.GenerateIPTV(number.ToString(), chatId);

            if (string.IsNullOrWhiteSpace(link) || !Uri.TryCreate(link, UriKind.Absolute, out Uri? uri))
            {
                await botClient.SendTextMessageAsync(chatId, "❌ Une erreur est survenue lors de la génération IPTV. Aucun débit n'a été effectué.");
                return;
            }

            if (!DataBase.DebiterSoldeAtomique(userId, prix, true, out _))
            {
                await botClient.SendTextMessageAsync(chatId, "❌ Solde insuffisant au moment du débit. Aucun abonnement n'a été confirmé côté boutique.");
                return;
            }

            string username = QueryParam(uri, "username", "user") ?? "";
            string password = QueryParam(uri, "password", "pass", "passwd") ?? "";

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (pathSegments.Length >= 3)
                {
                    if (string.IsNullOrWhiteSpace(username)) username = pathSegments[pathSegments.Length - 2];
                    if (string.IsNullOrWhiteSpace(password)) password = pathSegments[pathSegments.Length - 1].Split('.')[0];
                }
            }

            string baseUrl = "http://cf.business-cloud-neo.com";
            string safeBaseUrl = System.Net.WebUtility.HtmlEncode(baseUrl);
            string safeUsername = System.Net.WebUtility.HtmlEncode(username);
            string safePassword = System.Net.WebUtility.HtmlEncode(password);

            string iptvMessage = $"<b>📺 ChezRheyy IPTV</b>\n\n" +
                                 $"🌐 <b>Host :</b> <code>{safeBaseUrl}</code>\n" +
                                 $"👤 <b>Username :</b> <code>{safeUsername}</code>\n" +
                                 $"🔑 <b>Password :</b> <code>{safePassword}</code>";

            try
            {
                await botClient.SendTextMessageAsync(chatId, iptvMessage, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
            }
            catch
            {
                string plainMessage = $"ChezRheyy IPTV\n\nHost: {baseUrl}\nUsername: {username}\nPassword: {password}";
                await botClient.SendTextMessageAsync(chatId, plainMessage, cancellationToken: cancellationToken);
            }

            try
            {
                foreach (var id in config.idAdmins)
                {
                    await botClient.SendTextMessageAsync(id, $"📺 <b>[ACHAT IPTV]</b>\n<b>User</b>: <code>{chatId}</code>\n<b>Formule</b>: {number} mois ({prix}€)\n<b>Username</b>: <code>{safeUsername}</code>", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                }
            }
            catch { }

            DataBase.EnregistrerTransaction(userId, "IPTV", $"{number} mois", username, null, prix);
        }

        private static string? QueryParam(Uri uri, params string[] keys)
        {
            string q = uri.Query.TrimStart('?');
            foreach (var part in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = part.IndexOf('=');
                string k = Uri.UnescapeDataString(eq >= 0 ? part[..eq] : part);
                string v = eq >= 0 ? Uri.UnescapeDataString(part[(eq + 1)..]) : "";
                if (keys.Any(x => x.Equals(k, StringComparison.OrdinalIgnoreCase))) return v;
            }
            return null;
        }

        private static async Task SendIptvStock(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken, string chatId)
        {
            string p1 = config.GetSetting("iptv", "price_1m", "5");
            string p3 = config.GetSetting("iptv", "price_3m", "10");
            string p6 = config.GetSetting("iptv", "price_6m", "15");
            string p12 = config.GetSetting("iptv", "price_12m", "30");

            var message = $"*ChezRheyy IPTV* \n\n1 mois ➔ {p1}€\n3 mois ➔ {p3}€\n6 mois ➔ {p6}€\n12 mois ➔ {p12}€";

            var keyboardButtons = new List<List<InlineKeyboardButton>>
            {
                new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("1 mois", "iiptv1mois"),
                    InlineKeyboardButton.WithCallbackData("3 mois", "iiptv3mois"),
                },
                new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("6 mois","iiptv6mois"),
                    InlineKeyboardButton.WithCallbackData("12 mois","iiptv12mois"),
                },
                new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("Home", "iHome")
                }
            };

            await botClient.SendTextMessageAsync(chatId, message, parseMode: ParseMode.Markdown, replyMarkup: new InlineKeyboardMarkup(keyboardButtons));
        }

        private static async Task SendCarrefourStock(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken, string chatId)
        {
            var homeKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Home", "iHome") }
            });

            try
            {
                if (config.IdMessage.TryGetValue(chatId, out string? mid) && int.TryParse(mid, out int messageId))
                {
                    try { await botClient.DeleteMessageAsync(chatId, messageId); } catch { }
                }

                var stockList = DataBase.ObtenirStocksParBrand("carr");
                if (stockList.Count == 0)
                {
                    await botClient.SendTextMessageAsync(chatId, "😞 Aucun stock 'Carrefour', veuillez revenir plus tard", replyMarkup: homeKeyboard);
                    return;
                }

                var message = "*Stock Carrefour:* \n\n";
                var lignes = new List<List<InlineKeyboardButton>>();

                for (int i = 0; i < stockList.Count; i += 3)
                {
                    var ligne = new List<InlineKeyboardButton>();
                    for (int j = 0; j < 3 && (i + j) < stockList.Count; j++)
                    {
                        var item = stockList[i + j];
                        message += $"Solde:{item.Value} => {item.Price}€\n";
                        ligne.Add(InlineKeyboardButton.WithCallbackData($"{item.Value}", $"stock_{item.Id}"));
                    }
                    lignes.Add(ligne);
                }

                lignes.Add(new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("Home", "iHome")
                });

                await botClient.SendTextMessageAsync(chatId, message, parseMode: ParseMode.Markdown, replyMarkup: new InlineKeyboardMarkup(lignes));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendCarrefourStock] {ex.Message}");
                await botClient.SendTextMessageAsync(chatId, "Impossible d'afficher le stock pour le moment.", replyMarkup: homeKeyboard);
            }
        }
    }
}
