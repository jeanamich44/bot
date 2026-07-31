using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ChezRheyyBot
{
    internal class admin
    {
        public static List<string> command = new List<string>() { "/addMoney", "/removeMoney", "/ban", "/message","/info", "/stock", "/commandes","/help","/crypto","/unlock","/deban","/clear","/stat" };
        public static async Task<bool> CommandeAdmin(string message, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            string? commandeTrouvee = command.FirstOrDefault(cmd => message.Contains(cmd));

            if (commandeTrouvee != null)
            {
                switch (commandeTrouvee)
                {
                    case "/addMoney":
                        await AjouterArgent(message,botClient,update,cancellationToken);
                        break;
                    case "/removeMoney":
                        await RemoveMoney(message,botClient,update,cancellationToken);
                        break;
                    case "/ban":
                        await BanUser(botClient,update,cancellationToken);
                        break;
                    case "/deban":
                        await DebanUser(botClient,update,cancellationToken);
                        break;
                    case "/message":
                        await SendMessageAll(botClient,update,cancellationToken);
                        break;
                    case "/info":
                        await GetInFoUser(botClient,update,cancellationToken);
                        break;
                    case "/stock":
                        await ConnaitreNombreDeStock(botClient, update, cancellationToken);
                        break;
                    case "/commandes":
                        await RecupererAchatId(botClient,update,cancellationToken);
                        break;
                    case "/help":
                        await HelpCommande(botClient,update,cancellationToken);
                        break;
                    case "/crypto":
                        await GetInfoDePaiementId(botClient,update,cancellationToken);
                        break;

                    case "/unlock":
                        await UnLockPaiement(botClient,update,cancellationToken);
                        break;
                    case "/clear":

                        break;
                    case "/stat":
                        await SendStat(botClient,update,cancellationToken);
                        break;
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        private static async Task SendStat(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var lignes = System.IO.File.ReadAllLines("vendu.txt");

                // Dictionary pour stocker total + nombre de ventes
                Dictionary<string, (int total, int count)> stats = new Dictionary<string, (int total, int count)>();

                foreach (var line in lignes)
                {
                    try
                    {
                        var parts = line.Split('|');
                        string brand = parts[0].Split('=')[1].Trim();
                        int prix = int.Parse(parts[3].Split('=')[1].Trim());

                        if (stats.ContainsKey(brand))
                        {
                            var s = stats[brand];
                            stats[brand] = (s.total + prix, s.count + 1);
                        }
                        else
                        {
                            stats[brand] = (prix, 1);
                        }
                    }
                    catch
                    {

                    }
                }

                var message = new StringBuilder();
                message.AppendLine("📊 Statistiques des ventes :");
                foreach (var s in stats)
                {
                    message.AppendLine($"{s.Key} → {s.Value.count} ventes, total = {s.Value.total}");
                }

                await botClient.SendTextMessageAsync(config.CurrentChatId, message.ToString());
            }
            catch
            {

            }
        }

        private static async Task SendMessageAll(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var msg = update.Message.Text.Split('-');

                if(msg.Length != 1)
                {
                    foreach (var item in config.UserSave)
                    {
                        long userid = item.Item1;
                        try
                        {
                            botClient.SendTextMessageAsync(userid, msg[1]);
                        }
                        catch
                        {

                        }
                    }
                }
            }
            catch
            {

            }
        }

        private static async Task UnLockPaiement(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var msg = update.Message.Text.Split(' ');

                if(msg.Length == 2)
                {
                    if (config.banAPI.Contains(msg[1]))
                    {
                        config.banAPI.Remove(msg[1]);
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"User {msg[1]} peut payer");
                        return;
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"User {msg[1]} n'est pas bloquer");
                        return;
                    }
                }
            }
            catch
            {

            }
        }
        private static async Task AjouterArgent(string message, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var msg = message.Split(' ');

                if (msg.Length == 3)
                {
                    int result = config.UserSave.FindIndex(tuple => tuple.Item1 == long.Parse(msg[1]));

                    if (result != -1)
                    {
                        var ancienTuple = config.UserSave[result];
                        double nouveauSolde = ancienTuple.Item3 + double.Parse(msg[2]);

                        config.UserSave[result] = Tuple.Create(ancienTuple.Item1, ancienTuple.Item2, nouveauSolde, ancienTuple.Item4);
                        DataBase.SauvegarderUtilisateurs();
                        try
                        {
                            await botClient.SendTextMessageAsync(msg[1], $"💰 {msg[2]}€ reçus sur votre solde.");
                            await botClient.SendTextMessageAsync(config.CurrentChatId, $"Solde ajouter a {msg[1]}");
                            foreach(var id in config.idAdmins)
                            {
                                await botClient.SendTextMessageAsync(id, $"Solde mis a {msg[1]} montant {msg[2]}");
                            }

                            return;
                        }
                        catch
                        {
                            Console.WriteLine("[-] Impossible d'envoyer les message {Money}");
                            return;
                        }
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"ID:{msg[1]} introuvables.");
                        return;
                    }


                }

                await botClient.SendTextMessageAsync(config.CurrentChatId, "Erreur: Mauvais format ex: /addMoney <id> <montant>\n");
                return;
            }
            catch
            {

            }
        }

        private static async Task RemoveMoney(string message, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var msg = message.Split(' ');

                if (msg.Length == 3)
                {
                    int result = config.UserSave.FindIndex(tuple => tuple.Item1 == long.Parse(msg[1]));

                    if (result != -1)
                    {
                        var ancienTuple = config.UserSave[result];
                        double nouveauSolde = ancienTuple.Item3 - double.Parse(msg[2]);

                        if(nouveauSolde < 0)
                        {
                            config.UserSave[result] = Tuple.Create(ancienTuple.Item1, ancienTuple.Item2, 0.0, ancienTuple.Item4);
                        }
                        else
                        {
                            config.UserSave[result] = Tuple.Create(ancienTuple.Item1, ancienTuple.Item2, nouveauSolde, ancienTuple.Item4);

                        }
                        DataBase.SauvegarderUtilisateurs();
                        try
                        {
                            await botClient.SendTextMessageAsync(msg[1], $"Solde déduit de {msg[2]}€.");
                            await botClient.SendTextMessageAsync(config.CurrentChatId, $"Solde retiré à {msg[1]}");
                            return;
                        }
                        catch
                        {
                            return;
                        }
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"ID:{msg[1]} introuvables.");
                        return;
                    }


                }

                await botClient.SendTextMessageAsync(config.CurrentChatId, "Erreur: Mauvais format ex: /removeMoney <id> <montant>\n");
                return;
            }
            catch
            {

            }
        }

        private static async Task BanUser(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var msg = update.Message.Text.Split(' ');

                if(msg.Length != 2)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Erreur: Mauvais format ex: /ban <id>\n");
                    return;
                }

                if (config.BanniUser.Contains(msg[1]))
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"{msg[1]} est déjà banni.");
                    return;
                }

                config.BanniUser.Add(msg[1]);
                long userId = long.Parse(msg[1]);
                int idx = config.UserSave.FindIndex(u => u.Item1 == userId);
                if (idx != -1)
                {
                    var old = config.UserSave[idx];
                    config.UserSave[idx] = Tuple.Create(old.Item1, old.Item2, old.Item3, true);
                }
                else
                {
                    config.UserSave.Add(Tuple.Create(userId, 0, 0.0, true));
                }
                DataBase.SauvegarderUtilisateurs();

                await botClient.SendTextMessageAsync(config.CurrentChatId, $"L'ID {msg[1]} a bien été banni.");
                foreach (var id in config.idAdmins)
                {
                    await botClient.SendTextMessageAsync(id, $"BAN USER: {msg[1]}\n");
                }

                return;
            }
            catch
            {

            }
        }
        private static async Task DebanUser(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var msg = update.Message.Text.Split(' ');

                if (msg.Length != 2)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Erreur: Mauvais format ex: /deban <id>\n");
                    return;
                }

                if (config.BanniUser.Contains(msg[1]))
                {
                    config.BanniUser.Remove(msg[1]);
                }

                long userId = long.Parse(msg[1]);
                int idx = config.UserSave.FindIndex(u => u.Item1 == userId);
                if (idx != -1)
                {
                    var old = config.UserSave[idx];
                    config.UserSave[idx] = Tuple.Create(old.Item1, old.Item2, old.Item3, false);
                }
                DataBase.SauvegarderUtilisateurs();

                await botClient.SendTextMessageAsync(config.CurrentChatId, $"L'ID {msg[1]} a bien été débanni.");
                foreach (var id in config.idAdmins)
                {
                    await botClient.SendTextMessageAsync(id, $"DEBAN USER: {msg[1]}\n");
                }

                return;
            }
            catch
            {

            }
        }


        private static async Task GetInFoUser(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var msg = update.Message.Text.Split(' ');

                if(msg.Length == 2)
                {
                    int result = config.UserSave.FindIndex(tuple => tuple.Item1 == long.Parse(msg[1]));

                    if(result == -1)
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"Erreur: {msg[1]} ID introuvables !!");
                        return;
                    }

                    var ancienTuple = config.UserSave[result];
                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"Info de {msg[1]}\n\nAchat: {ancienTuple.Item2}\nSolde: {ancienTuple.Item3}\n");
                    return;
                }
            }
            catch
            {

            }
        }
        private static async Task ConnaitreNombreDeStock(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                //DataBase.StockItem Connaitre = new DataBase.StockItem();

                var Connaitre = DataBase.ObtenirStocksParBrand("quick");

                if(Connaitre.Count == 0)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Aucun STOCK !");
                    return;
                }

                await botClient.SendTextMessageAsync(config.CurrentChatId, $"Le stock es de {Connaitre.Count}\n", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                return;
            }
            catch
            {
                return;
            }
        }
        private static async Task RecupererAchatId(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var sd = "";

                var message = update.Message.Text.Split(' ');

                if(message.Length != 2)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Erreur: /commandes <id>\n");
                    return;
                }

                if (!System.IO.File.Exists("vendu.txt"))
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Erreur: Fichier d'achat introuvable");
                    return;
                }

                var read = System.IO.File.ReadAllLines("vendu.txt");
                if(read.Length == 0)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Aucun achat detecter , fichier vide ");
                    return;
                }

                for(int i = 0; i  < read.Length; i++)
                {
                    if (read[i].Contains(message[1]))
                    {
                        sd += $"{read[i]}\n";
                    }
                }

                if(sd.Length == 0)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"Aucune commandes pour {message[1]}");
                    return;
                }
                else
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"Historique achat de {message[1]}\n\n{sd}\n");
                    return;
                }
            }
            catch
            {
                return;
            }
        }
        private static async Task HelpCommande(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var commandmsg = "";
                for(int i = 0;i < command.Count; i++)
                {
                    commandmsg += command[i].ToString() + "\n";
                }

                await botClient.SendTextMessageAsync(config.CurrentChatId, $"*Listes des commandes*\n{commandmsg}",parseMode:Telegram.Bot.Types.Enums.ParseMode.Markdown);
            }
            catch
            {

            }
        }

        private static async Task GetInfoDePaiementId(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var id = update.Message.Text.Split(' ');
                if (id.Length != 2)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Erreur commandes: /crypto (trackId)");
                    return;
                }

                var httpClient = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.oxapay.com/v1/payment/{id[1]}");
                request.Headers.Add("merchant_api_key", config.apiKey);

                var response = await httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if(response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId,$"Transaction {id[1]} introuvables.");
                    return;
                }
                using JsonDocument doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var data))
                {
                    var status = data.GetProperty("status").GetString();
                    var montant = data.GetProperty("amount").GetDouble();

                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"Transaction {id[1]} [Montant={montant} | Status={status}]");
                    return;
                }
            }
            catch
            {

            }
        }

        private static async Task ClearCategorie(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var categorie = update.Message.Text.Split(' ');
            if (categorie.Length != 2)
            {
                await botClient.SendTextMessageAsync(config.CurrentChatId, "Erreur commandes: /clear (categorie)");
                return;
            }


        }
    }
}
