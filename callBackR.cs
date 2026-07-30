using System.Drawing.Drawing2D;
using System.Security.Policy;
using System.Web;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace UgcBotTG
{
    internal class callBackR
    {
        public static async Task ResponseCallBack(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if(update.CallbackQuery.Data == null)
                {
                    return;
                }

                if (update.CallbackQuery.Data == "iQuick")
                {
                    await SendQuickStock(botClient, update, cancellationToken);
                    return;
                }
                else if (update.CallbackQuery.Data == "iCarrefour")
                {
                    await SendCarrefourStock(botClient, update, cancellationToken);
                    return;
                }

                else if (update.CallbackQuery.Data == "iFlunch")
                {
                    await SendFlunchStock(botClient, update, cancellationToken);
                    return;
                }
                else if (update.CallbackQuery.Data == "iKFC")
                {
                    await SendKfcStock(botClient, update, cancellationToken);
                    return;
                }
                else if (update.CallbackQuery.Data == "iMonoprix")
                {
                    await SendMonoprixStock(botClient, update, cancellationToken);
                    return;
                }
                else if (update.CallbackQuery.Data == "iFnac")
                {
                    await SendFnacStock(botClient, update, cancellationToken);
                    return;
                }
                else if (update.CallbackQuery.Data == "iIPTV")
                {
                    await SendIptvStock(botClient, update, cancellationToken);
                    return;
                }
                else if(update.CallbackQuery.Data == "iCinema")
                {
                    await SendCinemaStock(botClient, update, cancellationToken);
                    return;
                }

                //cinema
                else if (update.CallbackQuery.Data == "iPatheGaumont")
                {
                    //acheter un CinePass Gaumont
                    await cinema.AcheterCinema(botClient, update, cancellationToken);
                    return;
                }
                else if (update.CallbackQuery.Data == "iUgc")
                {
                    await cinema.AcheterCinema(botClient, update, cancellationToken);
                    //acheter un CinePass UGC
                    return;
                }

                else if (update.CallbackQuery.Data.Contains("iiptv"))
                {


                    Dictionary<int, double> prixParMois = new()
                    {
                        { 1, 5 },
                        { 3, 10 },
                        { 6, 15 },
                        { 12, 30 }
                    };

                    var msg = update.CallbackQuery.Data;

                    // Extraction du nombre (mois)
                    if (!int.TryParse(new string(msg.Where(char.IsDigit).ToArray()), out int number))
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, "Offre invalide.");
                        return;
                    }

                    // Recherche utilisateur
                    int index = config.UserSave.FindIndex(t => t.Item1 == long.Parse(config.CurrentChatId));
                    if (index == -1)
                    {
                        return;
                    }

                    var user = config.UserSave[index];
                    double solde = user.Item3;

                    // Vérification prix
                    if (!prixParMois.TryGetValue(number, out double prix))
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, "Abonnement non disponible.");
                        return;
                    }

                    // Vérification solde
                    if (solde < prix)
                    {
                        await botClient.SendTextMessageAsync(
                            config.CurrentChatId,
                            $"Solde insuffisant ❌\nPrix : {prix}€\nVotre solde : {solde}€"
                        );
                        return;
                    }

                    // Génération IPTV
                    string link = await iptv.GenerateIPTV(number.ToString());

                    // Mise à jour du solde
                    double nouveauSolde = solde - prix;
                    config.UserSave[index] = Tuple.Create(
                        user.Item1,
                        user.Item2 + 1,
                        nouveauSolde
                    );


                    try
                    {
                        foreach (var id in config.idAdmins)
                        {
                            await botClient.SendTextMessageAsync(id, $"[+] Achat IPTV | Id:{config.CurrentChatId} | Montant:{prix}");
                        }
                    }
                    catch
                    {

                    }

                    // Envoi du lien
                    Uri uri = new Uri(link);
                    var query = HttpUtility.ParseQueryString(uri.Query);

                   // string baseUrl = $"{uri.Scheme}://{uri.Host}";
                    string baseUrl = "http://cf.business-cloud-neo.ru/";
                    string username = query["username"];
                    string password = query["password"];
                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"*ChezRheyy IPTV*\n\nHost:{baseUrl}\nUsername:{username}\nPassword:{password}\n", parseMode: ParseMode.Markdown);

                    System.IO.File.AppendAllText("vendu.txt", $"Brand = IPTV | Carte = {number} | Solde = 0 | Prix = {prix} | Id = 0\n");
                }

                else if (update.CallbackQuery.Data == "iCanal")
                {
                    //await SendCanal(botClient, update, cancellationToken);
                    return;
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
                else if (update.CallbackQuery.Data.Contains("iProfile"))
                {
                    await profile.AfficherProfile(botClient, update, cancellationToken);
                    return;
                } else if (update.CallbackQuery.Data.Contains("iRemoveParrain"))
                {
                    await SampleM.RemoveParain(botClient, update, cancellationToken);
                    return;
                }
                else if (update.CallbackQuery.Data.Contains("iParainConfig"))
                {
                    await SampleM.PreparationCodeParain(botClient, update, cancellationToken);
                    return;
                }
                else if (update.CallbackQuery.Data.Contains("iCustomP"))
                {
                    await paiement.RecupererMontant(botClient, update, cancellationToken);
                    return;
                }
                else if (update.CallbackQuery.Data.Contains("iCustomCB"))
                {
                    if (config.PayementAPI.ContainsKey(config.CurrentChatId))
                    {
                        // var link = config.PayementAPI[config.CurrentChatId];
                        //await botClient.SendTextMessageAsync(config.CurrentChatId, $"Un Liens de paiement es deja en cours pour vous. {link}\n");
                        //return;
                    }
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

                //gerer les paiement

                else if (update.CallbackQuery.Data.Contains("iPay_")) {
                   // await paiement.GenerateLink(botClient, update, cancellationToken);
                    return;
                }
            }
            catch
            {

            }

            return;
        }



        //envoyer les message
        private static async Task SendQuickStock(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var message = "*Stock QUICK:* \n\n";

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

                var stockList = DataBase.ObtenirStocksParBrand("quick");

                if (stockList.Count == 0)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"😞 Aucun stock 'Quick', veuillez revenir plus tard",replyMarkup:inlineKeyboard);
                    return;
                }

                var lignes = new List<List<InlineKeyboardButton>>();

                for (int i = 0; i < stockList.Count; i += 3)
                {
                    var ligne = new List<InlineKeyboardButton>();

                    for (int j = 0; j < 3 && (i + j) < stockList.Count; j++)
                    {
                        var item = stockList[i + j];

                        message += $"Points:{item.Value} => {item.Price}€\n";
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
                    parseMode:Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: keyboard
                );

                //await botClient.SendTextMessageAsync(config.CurrentChatId, "Stock de Quick", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                return;

            }
            catch
            {
                

                await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]));

                var stockList = DataBase.ObtenirStocksParBrand("quick");

                if (stockList.Count == 0)
                {

                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"😞 Aucun stock 'Quick', veuillez revenir plus tard", replyMarkup: inlineKeyboard);
                    return;
                }

                var lignes = new List<List<InlineKeyboardButton>>();

                for (int i = 0; i < stockList.Count; i += 3)
                {
                    var ligne = new List<InlineKeyboardButton>();

                    for (int j = 0; j < 3 && (i + j) < stockList.Count; j++)
                    {
                        var item = stockList[i + j];

                        message += $"Points:{item.Value} => {item.Price}€\n";
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

        private static async Task SendFlunchStock(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var message = "*Stock Flunch:* \n\n";

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

                var stockList = DataBase.ObtenirStocksParBrand("flunch");

                if (stockList.Count == 0)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"😞 Aucun stock 'flunch', veuillez revenir plus tard", replyMarkup: inlineKeyboard);
                    return;
                }

                var lignes = new List<List<InlineKeyboardButton>>();

                for (int i = 0; i < stockList.Count; i += 3)
                {
                    var ligne = new List<InlineKeyboardButton>();

                    for (int j = 0; j < 3 && (i + j) < stockList.Count; j++)
                    {
                        var item = stockList[i + j];

                        message += $"Points:{item.Value} => {item.Price}€\n";
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

                //await botClient.SendTextMessageAsync(config.CurrentChatId, "Stock de Quick", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                return;

            }
            catch
            {


                await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]));

                var stockList = DataBase.ObtenirStocksParBrand("flunch");

                if (stockList.Count == 0)
                {

                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"😞 Aucun stock 'flunch', veuillez revenir plus tard", replyMarkup: inlineKeyboard);
                    return;
                }

                var lignes = new List<List<InlineKeyboardButton>>();

                for (int i = 0; i < stockList.Count; i += 3)
                {
                    var ligne = new List<InlineKeyboardButton>();

                    for (int j = 0; j < 3 && (i + j) < stockList.Count; j++)
                    {
                        var item = stockList[i + j];

                        message += $"Points:{item.Value} => {item.Price}€\n";
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


        private static async Task SendIptvStock(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var message = "*ChezRheyy IPTV* \n\n1 mois ➔ 5€\n3 mois ➔ 10€\n6 mois ➔ 15€\n12 mois ➔ 30€";

            var inlineKeyboard = new InlineKeyboardMarkup(new[]
          {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Home", "iHome")
                    }
                });

            try
            {

                var keyboardButtons = new List<List<InlineKeyboardButton>>
{
   new List<InlineKeyboardButton>
                        {
                            InlineKeyboardButton.WithCallbackData("1 mois", "iiptv1mois"),
                            InlineKeyboardButton.WithCallbackData("3 mois","iiptv3mois "),

                        }
                    };

                keyboardButtons.Add(new List<InlineKeyboardButton>
                                        {
                                         InlineKeyboardButton.WithCallbackData("6 mois","iiptv6mois"),
                                         InlineKeyboardButton.WithCallbackData("12 mois","iiptv12mois"),
                                    });


                var inlineKeyboards = new InlineKeyboardMarkup(keyboardButtons);

                await botClient.SendTextMessageAsync(config.CurrentChatId,message, parseMode: ParseMode.MarkdownV2, replyMarkup: inlineKeyboards);
                return;
            }
            catch
            {

            }
        }




        private static async Task SendFnacStock(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var message = "*Stock Fnac:* \n\n";

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

                var stockList = DataBase.ObtenirStocksParBrand("fnac");

                if (stockList.Count == 0)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"😞 Aucun stock 'fnac', veuillez revenir plus tard", replyMarkup: inlineKeyboard);
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

                //await botClient.SendTextMessageAsync(config.CurrentChatId, "Stock de Quick", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                return;

            }
            catch
            {


                await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]));

                var stockList = DataBase.ObtenirStocksParBrand("fnac");

                if (stockList.Count == 0)
                {

                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"😞 Aucun stock 'fnac', veuillez revenir plus tard", replyMarkup: inlineKeyboard);
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


        //monoprix




        private static async Task SendMonoprixStock(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var message = "*Stock Monoprix:* \n\n";

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

                var stockList = DataBase.ObtenirStocksParBrand("monoprix");

                if (stockList.Count == 0)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"😞 Aucun stock 'Monoprix', veuillez revenir plus tard", replyMarkup: inlineKeyboard);
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

                //await botClient.SendTextMessageAsync(config.CurrentChatId, "Stock de Quick", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                return;

            }
            catch
            {


                await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]));

                var stockList = DataBase.ObtenirStocksParBrand("monoprix");

                if (stockList.Count == 0)
                {

                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"😞 Aucun stock 'Monoprix', veuillez revenir plus tard", replyMarkup: inlineKeyboard);
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

        private static async Task SendKfcStock(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var message = "*Stock Accor Hotel (logs):* \n\n";

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

                var stockList = DataBase.ObtenirStocksParBrand("accor");

                if (stockList.Count == 0)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"😞 Aucun stock 'AccorHotel', veuillez revenir plus tard", replyMarkup: inlineKeyboard);
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

                //await botClient.SendTextMessageAsync(config.CurrentChatId, "Stock de Quick", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                return;

            }
            catch
            {


                await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]));

                var stockList = DataBase.ObtenirStocksParBrand("accor");

                if (stockList.Count == 0)
                {

                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"😞 Aucun stock 'AccorHotel', veuillez revenir plus tard", replyMarkup: inlineKeyboard);
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





        private static async Task SendCanal(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var inlineKeyboard = new InlineKeyboardMarkup(new[]
          {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Home", "iHome")                    }
                });
                //envoyer le canal pour s'abonner
                await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]));

                await botClient.SendTextMessageAsync(config.CurrentChatId, "Voici notre canal: https://t.me/chezquickk",replyMarkup:inlineKeyboard);
            }
            catch
            {
                await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]) - 1);


                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                          {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Home", "iHome")                    }
                });
                await botClient.SendTextMessageAsync(config.CurrentChatId, "Voici notre canal: https://t.me/chezquickk", replyMarkup: inlineKeyboard);
            }
        }



        //gere le stock carrefour
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

                //await botClient.SendTextMessageAsync(config.CurrentChatId, "Stock de Quick", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
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

        private static async Task SendCinemaStock(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {

                await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]) - 1);

                var PatheStock = DataBase.ObtenirStocksParBrand("Pathe");
                var ugcStock = DataBase.ObtenirStocksParBrand("Ugc");

                if (PatheStock.Count == 0 && ugcStock.Count == 0)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "😞 Aucun stock 'cinema' disponible.");
                    return;
                }

               // await botClient.SendTextMessageAsync(config.CurrentChatId, $"Stock Cinema:");


                var lignes = new List<List<InlineKeyboardButton>>();

                
                    var ligne = new List<InlineKeyboardButton>();



                if(PatheStock.Count > 0)
                {
                    ligne.Add(InlineKeyboardButton.WithCallbackData("✅ CinePass Pathe", "iPatheGaumont"));
                }
                else
                {
                    ligne.Add(InlineKeyboardButton.WithCallbackData("❌ CinePass Pathe", "iPatheGaumont"));
                }

                if(ugcStock.Count > 0)
                {
                    ligne.Add(InlineKeyboardButton.WithCallbackData("✅ CinePass UGC", "iUgc"));
                }
                else
                {
                    ligne.Add(InlineKeyboardButton.WithCallbackData("❌ CinePass UGC", "iUgc"));
                }

                lignes.Add(ligne);

                lignes.Add(new List<InlineKeyboardButton>
{
    InlineKeyboardButton.WithCallbackData("Home", "iHome")
    });

                var keyboard = new InlineKeyboardMarkup(lignes);


                await botClient.SendTextMessageAsync(
                  chatId: config.CurrentChatId,
                   text: "Stock Cinema:\nPrix: 55€",
                  parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                  replyMarkup: keyboard
              );

            }
            catch
            {
                await botClient.DeleteMessageAsync(config.CurrentChatId, int.Parse(config.IdMessage[config.CurrentChatId]));

                var PatheStock = DataBase.ObtenirStocksParBrand("Pathe");
                var ugcStock = DataBase.ObtenirStocksParBrand("Ugc");

                if (PatheStock.Count == 0 && ugcStock.Count == 0)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "😞 Aucun stock 'cinema' disponible.");
                    return;
                }

                var lignes = new List<List<InlineKeyboardButton>>();


                var ligne = new List<InlineKeyboardButton>();



                if (PatheStock.Count > 0)
                {
                    ligne.Add(InlineKeyboardButton.WithCallbackData("✅ CinePass Pathe", "iPatheGaumont"));
                }
                else
                {
                    ligne.Add(InlineKeyboardButton.WithCallbackData("❌ CinePass Pathe", "iPatheGaumont"));
                }

                if (ugcStock.Count > 0)
                {
                    ligne.Add(InlineKeyboardButton.WithCallbackData("✅ CinePass UGC", "iUgc"));
                }
                else
                {
                    ligne.Add(InlineKeyboardButton.WithCallbackData("❌ CinePass UGC", "iUgc"));
                }

                lignes.Add(ligne);

                lignes.Add(new List<InlineKeyboardButton>
{
    InlineKeyboardButton.WithCallbackData("Home", "iHome")
    });

                var keyboard = new InlineKeyboardMarkup(lignes);

               // await botClient.SendTextMessageAsync(config.CurrentChatId, $"Stock Cinema:");

                await botClient.SendTextMessageAsync(
                   chatId: config.CurrentChatId,
                   text: "Stock Cinema:\nPrix: 55€",
                   parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                   replyMarkup: keyboard
               );
            }
        }
    }
}
