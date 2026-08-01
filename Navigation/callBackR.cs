using System.Drawing.Drawing2D;
using System.Security.Policy;
using System.Web;
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
                config.IncCommandsExecuted();
                if (update.CallbackQuery.Data == null)
                {
                    return;
                }
                else if (update.CallbackQuery.Data == "iCarrefour")
                {
                    await SendCarrefourStock(botClient, update, cancellationToken);
                    return;
                }
                else if (update.CallbackQuery.Data == "iIPTV")
                {
                    await SendIptvStock(botClient, update, cancellationToken);
                    return;
                }
                else if (update.CallbackQuery.Data.Contains("iiptv"))
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

                    var msg = update.CallbackQuery.Data;

                    if (!int.TryParse(new string(msg.Where(char.IsDigit).ToArray()), out int number))
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, "Offre invalide.");
                        return;
                    }

                    int index = config.UserSave.FindIndex(t => t.Item1 == long.Parse(config.CurrentChatId));
                    if (index == -1)
                    {
                        return;
                    }

                    var user = config.UserSave[index];
                    double solde = user.Item3;

                    if (!prixParMois.TryGetValue(number, out double prix))
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, "Abonnement non disponible.");
                        return;
                    }

                    if (solde < prix)
                    {
                        await botClient.SendTextMessageAsync(
                            config.CurrentChatId,
                            $"Solde insuffisant ❌\nPrix : {prix}€\nVotre solde : {solde}€"
                        );
                        return;
                    }

                    string link = await iptv.GenerateIPTV(number.ToString());

                    double nouveauSolde = solde - prix;
                    config.UserSave[index] = Tuple.Create(
                        user.Item1,
                        user.Item2 + 1,
                        nouveauSolde,
                        user.Item4
                    );

                    try
                    {
                        foreach (var id in config.idAdmins)
                        {
                            await botClient.SendTextMessageAsync(id, $"[+] Achat IPTV | Id:{config.CurrentChatId} | Montant:{prix}");
                        }
                    }
                    catch { }

                    Uri uri = new Uri(link);
                    var query = HttpUtility.ParseQueryString(uri.Query);

                    string baseUrl = $"{uri.Scheme}://{uri.Host}";
                    string username = query["username"];
                    string password = query["password"];
                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"*ChezRheyy IPTV*\n\nHost:{baseUrl}\nUsername:{username}\nPassword:{password}\n", parseMode: ParseMode.Markdown);

                    long.TryParse(config.CurrentChatId, out long userIdIptv);
                    DataBase.EnregistrerTransaction(userIdIptv, "IPTV", number.ToString(), "", 0, prix);
                }
                else if (update.CallbackQuery.Data == "iHome")
                {
                    await SampleM.SendMessage(botClient, update, cancellationToken);
                    return;
                }
                else if (update.CallbackQuery.Data == "iPaiement")
                {
                    await paiement.PaiementList(botClient, update, cancellationToken);
                    return;
                }
                else if (update.CallbackQuery.Data.Contains("stock_"))
                {
                    await achat.DemandeAchat("", botClient, update, cancellationToken);
                    return;
                }
                else if (update.CallbackQuery.Data.Contains("iCustomP"))
                {
                    if (config.banAPI.Contains(config.CurrentChatId))
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, "Il ne sera plus possible de créer des liens pendant 24 heures. Vous serez informé dès que vous pourrez en créer un.");
                        return;
                    }

                    if (!config.CustomPaiement.Contains(config.CurrentChatId))
                    {
                        config.CustomPaiement.Add(config.CurrentChatId);
                    }
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Merci de rentrer un montant à recharger");
                    return;
                }
                else if (update.CallbackQuery.Data.StartsWith("iPayAmt_"))
                {
                    string montant = update.CallbackQuery.Data.Replace("iPayAmt_", "");
                    await paiement.GenerateLink(botClient, update, cancellationToken, montant);
                    return;
                }
                else if (update.CallbackQuery.Data.Contains("iMontantPersoCrypto"))
                {
                    await paiement.ActiverSaisieCustom(botClient, update, cancellationToken);
                    return;
                }
                else if (update.CallbackQuery.Data.Contains("iCancelPaiement"))
                {
                    await paiement.AnnulerPaiement(botClient, update, cancellationToken);
                    return;
                }
                else if (update.CallbackQuery.Data.Contains("iCustomCB"))
                {
                    if (config.banAPI.Contains(config.CurrentChatId))
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, "Il ne sera plus possible de créer des liens pendant 24 heures. Vous serez informé dès que vous pourrez en créer un.");
                        return;
                    }

                    if (config.AttentePaiement.Contains(config.CurrentChatId))
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, "Merci de rentrer un montant à recharger");
                        return;
                    }

                    config.AttentePaiement.Add(config.CurrentChatId);
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Merci de rentrer un montant à recharger");
                    return;
                }
            }
            catch { }

            return;
        }

        private static async Task SendIptvStock(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            string p1 = config.GetSetting("iptv", "price_1m", "5");
            string p3 = config.GetSetting("iptv", "price_3m", "10");
            string p6 = config.GetSetting("iptv", "price_6m", "15");
            string p12 = config.GetSetting("iptv", "price_12m", "30");

            var message = $"*ChezRheyy IPTV* \n\n1 mois ➔ {p1}€\n3 mois ➔ {p3}€\n6 mois ➔ {p6}€\n12 mois ➔ {p12}€";

            try
            {
                var keyboardButtons = new List<List<InlineKeyboardButton>>
                {
                    new List<InlineKeyboardButton>
                    {
                        InlineKeyboardButton.WithCallbackData("1 mois", "iiptv1mois"),
                        InlineKeyboardButton.WithCallbackData("3 mois","iiptv3mois "),
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

                var inlineKeyboards = new InlineKeyboardMarkup(keyboardButtons);

                await botClient.SendTextMessageAsync(config.CurrentChatId, message, parseMode: ParseMode.Markdown, replyMarkup: inlineKeyboards);
                return;
            }
            catch { }
        }

        private static async Task SendCarrefourStock(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var message = "*Stock Carrefour:* \n\n";

            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Home", "iHome")
                }
            });

            try
            {
                await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]) - 1);

                var stockList = DataBase.ObtenirStocksParBrand("carr");

                if (stockList.Count == 0)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"😞 Aucun stock 'Carrefour', veuillez revenir plus tard", replyMarkup: inlineKeyboard);
                    return;
                }

                var lignes = new List<List<InlineKeyboardButton>>();

                for (int i = 0; i < stockList.Count; i += 3)
                {
                    var ligne = new List<InlineKeyboardButton>();

                    for (int j = 0; j < 3 && (i + j) < stockList.Count; j++)
                    {
                        var item = stockList[i + j];

                        message += $"Solde:{item.Value} => {item.Price}€\n";
                        string texte = $"{item.Value}";
                        string callback = $"stock_{item.Id}";

                        ligne.Add(InlineKeyboardButton.WithCallbackData(texte, callback));
                    }

                    lignes.Add(ligne);
                }

                lignes.Add(new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("Home", "iHome")
                });
                var keyboard = new InlineKeyboardMarkup(lignes);

                await botClient.SendTextMessageAsync(
                    chatId: config.CurrentChatId,
                    text: message,
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: keyboard
                );

                return;
            }
            catch
            {
                await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]));

                var stockList = DataBase.ObtenirStocksParBrand("carr");

                if (stockList.Count == 0)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"😞 Aucun stock 'Carrefour', veuillez revenir plus tard", replyMarkup: inlineKeyboard);
                    return;
                }

                var lignes = new List<List<InlineKeyboardButton>>();

                for (int i = 0; i < stockList.Count; i += 3)
                {
                    var ligne = new List<InlineKeyboardButton>();

                    for (int j = 0; j < 3 && (i + j) < stockList.Count; j++)
                    {
                        var item = stockList[i + j];

                        message += $"Solde:{item.Value} => {item.Price}€\n";
                        string texte = $"{item.Value}";
                        string callback = $"stock_{item.Id}";

                        ligne.Add(InlineKeyboardButton.WithCallbackData(texte, callback));
                    }

                    lignes.Add(ligne);
                }

                lignes.Add(new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("Home", "iHome")
                });

                var keyboard = new InlineKeyboardMarkup(lignes);

                await botClient.SendTextMessageAsync(
                    chatId: config.CurrentChatId,
                    text: message,
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: keyboard
                );

                return;
            }
        }
    }
}
