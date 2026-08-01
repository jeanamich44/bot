using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ChezRheyyBot
{
    internal class admin
    {
        public static List<string> command = new List<string>()
        {
            "/addMoney",
            "/removeMoney",
            "/addstock",
            "/addStock",
            "/ban",
            "/deban",
            "/unlock",
            "/stat",
            "/stock",
            "/commandes",
            "/info",
            "/crypto",
            "/message",
            "/maintenance",
            "/panel",
            "/help"
        };

        private static readonly Dictionary<string, (string description, string exemple, string usage)> HelpDetails = new(StringComparer.OrdinalIgnoreCase)
        {
            { "addmoney", ("Ajoute du solde en euros à un utilisateur Telegram.", "/addMoney 123456789 25", "/addMoney <id> <montant>") },
            { "removemoney", ("Retire du solde en euros à un utilisateur.", "/removeMoney 123456789 10", "/removeMoney <id> <montant>") },
            { "addstock", ("Ajoute du stock en BDD depuis un fichier .txt joint (ex: CODE:PIN:VALEUR:PRIX).", "Envoyer un .txt avec la légende /addstock carr", "/addstock [marque]") },
            { "ban", ("Bannit un utilisateur avec une raison optionnelle.", "/ban 123456789 ou /ban 123456789 Spam / Arnaque", "/ban <id> [raison...]") },
            { "deban", ("Débannit un utilisateur préalablement banni.", "/deban 123456789", "/deban <id>") },
            { "unlock", ("Débloque la génération de liens de paiement pour un utilisateur restreint.", "/unlock 123456789", "/unlock <id>") },
            { "stat", ("Affiche les statistiques globales des ventes et le CA par marque depuis la BDD.", "/stat", "/stat") },
            { "stock", ("Affiche la quantité de stock actuellement disponible pour une marque (ex: /stock carr).", "/stock carr", "/stock [marque]") },
            { "commandes", ("Recherche l'historique des achats par ID utilisateur, nom de marque ou code carte.", "/commandes 123456789", "/commandes <id|marque|code>") },
            { "info", ("Affiche les informations d'un utilisateur (solde, nombre d'achats, statut banni).", "/info 123456789", "/info <id>") },
            { "crypto", ("Interroge l'API OxaPay pour connaître le statut en direct d'une transaction.", "/crypto track_123456", "/crypto <trackId>") },
            { "message", ("Envoie un message de diffusion (broadcast) à tous les utilisateurs du bot.", "/message - Bonjour à tous !", "/message - <texte>") },
            { "maintenance", ("Active ou désactive le mode maintenance du bot.", "/maintenance on ou /maintenance off", "/maintenance [on|off]") },
            { "panel", ("Affiche l'URL secrète d'accès au Panel d'Administration Web.", "/panel", "/panel") },
            { "help", ("Affiche l'aide des commandes administration.", "/help ou /help stock ou /help all", "/help [commande|all]") }
        };

        public static async Task<bool> CommandeAdmin(string message, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (!config.idAdmins.Contains(config.CurrentChatId)) return false;
            if (string.IsNullOrWhiteSpace(message)) return false;

            string firstWord = message.Split(' ')[0].Trim();
            string? commandeTrouvee = command.FirstOrDefault(cmd => cmd.Equals(firstWord, StringComparison.OrdinalIgnoreCase));

            if (commandeTrouvee != null)
            {
                switch (commandeTrouvee.ToLower())
                {
                    case "/addmoney":
                        await AjouterArgent(message, botClient, update, cancellationToken);
                        break;
                    case "/removemoney":
                        await RemoveMoney(message, botClient, update, cancellationToken);
                        break;
                    case "/addstock":
                        await AddStockFromFile(message, botClient, update, cancellationToken);
                        break;
                    case "/ban":
                        await BanUser(botClient, update, cancellationToken);
                        break;
                    case "/deban":
                        await DebanUser(botClient, update, cancellationToken);
                        break;
                    case "/message":
                        await SendMessageAll(botClient, update, cancellationToken);
                        break;
                    case "/maintenance":
                        await ToggleMaintenance(message, botClient, update, cancellationToken);
                        break;
                    case "/panel":
                        await SendPanelUrl(botClient, update, cancellationToken);
                        break;
                    case "/info":
                        await GetInFoUser(botClient, update, cancellationToken);
                        break;
                    case "/stock":
                        await ConnaitreNombreDeStock(message, botClient, update, cancellationToken);
                        break;
                    case "/commandes":
                        await RecupererAchatId(botClient, update, cancellationToken);
                        break;
                    case "/help":
                        await HelpCommande(botClient, update, cancellationToken);
                        break;
                    case "/crypto":
                        await GetInfoDePaiementId(botClient, update, cancellationToken);
                        break;
                    case "/unlock":
                        await UnLockPaiement(botClient, update, cancellationToken);
                        break;
                    case "/stat":
                        await SendStat(botClient, update, cancellationToken);
                        break;
                }

                return true;
            }
            return false;
        }

        private static async Task SendStat(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var transactions = DataBase.ObtenirTransactions();
                Dictionary<string, (double total, int count)> stats = new Dictionary<string, (double total, int count)>();

                foreach (var tx in transactions)
                {
                    string brand = string.IsNullOrEmpty(tx.Brand) ? "Inconnu" : tx.Brand;
                    if (stats.ContainsKey(brand))
                    {
                        var s = stats[brand];
                        stats[brand] = (s.total + tx.Price, s.count + 1);
                    }
                    else
                    {
                        stats[brand] = (tx.Price, 1);
                    }
                }

                var message = new StringBuilder();
                message.AppendLine("📊 Statistiques des ventes (BDD PostgreSQL) :");
                if (stats.Count == 0)
                {
                    message.AppendLine("Aucune vente enregistrée.");
                }
                else
                {
                    foreach (var s in stats)
                    {
                        message.AppendLine($"{s.Key} → {s.Value.count} ventes, total = {s.Value.total}€");
                    }
                }

                await botClient.SendTextMessageAsync(config.CurrentChatId, message.ToString(), cancellationToken: cancellationToken);
            }
            catch (Exception ex) { Console.WriteLine($"[Admin Erreur] {ex.Message}"); }
        }

        private static async Task SendMessageAll(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                string text = update.Message.Text ?? "";
                int index = text.IndexOf('-');
                if (index != -1 && index + 1 < text.Length)
                {
                    string contenu = text.Substring(index + 1).Trim();
                    foreach (var item in config.UserSave.ToList())
                    {
                        try
                        {
                            await botClient.SendTextMessageAsync(item.Item1, contenu, cancellationToken: cancellationToken);
                            await Task.Delay(35);
                        }
                        catch { }
                    }
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Message envoyé à tous les utilisateurs.", cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Erreur format: /message - <votre message>", cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex) { Console.WriteLine($"[Admin Erreur] {ex.Message}"); }
        }

        private static async Task UnLockPaiement(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var msg = update.Message.Text.Split(' ');
                if (msg.Length == 2)
                {
                    if (config.banAPI.Contains(msg[1]))
                    {
                        config.banAPI.Remove(msg[1]);
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"User {msg[1]} peut payer", cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"User {msg[1]} n'est pas bloqué", cancellationToken: cancellationToken);
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"[Admin Erreur] {ex.Message}"); }
        }

        private static async Task AjouterArgent(string message, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var msg = message.Split(' ');
                if (msg.Length == 3 && long.TryParse(msg[1], out long userId) && double.TryParse(msg[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double mtn))
                {
                    int result = config.UserSave.FindIndex(tuple => tuple.Item1 == userId);
                    if (result != -1)
                    {
                        var ancienTuple = config.UserSave[result];
                        double nouveauSolde = ancienTuple.Item3 + mtn;

                        config.UserSave[result] = Tuple.Create(ancienTuple.Item1, ancienTuple.Item2, nouveauSolde, ancienTuple.Item4);
                        DataBase.SauvegarderUtilisateurIndividuel(userId);

                        try
                        {
                            await botClient.SendTextMessageAsync(userId, $"💰 {mtn}€ reçus sur votre solde.", cancellationToken: cancellationToken);
                            await botClient.SendTextMessageAsync(config.CurrentChatId, $"Solde ajouté à {userId}", cancellationToken: cancellationToken);
                            foreach (var id in config.idAdmins)
                            {
                                await botClient.SendTextMessageAsync(id, $"Solde mis à {userId} montant {mtn}€", cancellationToken: cancellationToken);
                            }
                            return;
                        }
                        catch
                        {
                            Console.WriteLine("[-] Impossible d'envoyer le message de solde");
                            return;
                        }
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"ID:{userId} introuvable.", cancellationToken: cancellationToken);
                        return;
                    }
                }

                await botClient.SendTextMessageAsync(config.CurrentChatId, "Erreur: Mauvais format ex: /addMoney <id> <montant>", cancellationToken: cancellationToken);
            }
            catch (Exception ex) { Console.WriteLine($"[Admin Erreur] {ex.Message}"); }
        }

        private static async Task RemoveMoney(string message, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var msg = message.Split(' ');
                if (msg.Length == 3 && long.TryParse(msg[1], out long userId) && double.TryParse(msg[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double mtn))
                {
                    int result = config.UserSave.FindIndex(tuple => tuple.Item1 == userId);
                    if (result != -1)
                    {
                        var ancienTuple = config.UserSave[result];
                        double nouveauSolde = ancienTuple.Item3 - mtn;
                        if (nouveauSolde < 0) nouveauSolde = 0.0;

                        config.UserSave[result] = Tuple.Create(ancienTuple.Item1, ancienTuple.Item2, nouveauSolde, ancienTuple.Item4);
                        DataBase.SauvegarderUtilisateurIndividuel(userId);

                        try
                        {
                            await botClient.SendTextMessageAsync(userId, $"Solde déduit de {mtn}€.", cancellationToken: cancellationToken);
                            await botClient.SendTextMessageAsync(config.CurrentChatId, $"Solde retiré à {userId}", cancellationToken: cancellationToken);
                            return;
                        }
                        catch { return; }
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"ID:{userId} introuvable.", cancellationToken: cancellationToken);
                        return;
                    }
                }

                await botClient.SendTextMessageAsync(config.CurrentChatId, "Erreur: Mauvais format ex: /removeMoney <id> <montant>", cancellationToken: cancellationToken);
            }
            catch (Exception ex) { Console.WriteLine($"[Admin Erreur] {ex.Message}"); }
        }

        private static async Task BanUser(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var msg = update.Message.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (msg.Length < 2 || !long.TryParse(msg[1], out long userId))
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Erreur: Mauvais format ex: /ban <id> [raison]", cancellationToken: cancellationToken);
                    return;
                }

                string reason = msg.Length > 2 ? string.Join(" ", msg.Skip(2)) : "";

                if (config.BanniUser.Contains(msg[1]))
                {
                    if (!string.IsNullOrEmpty(reason))
                    {
                        config.BanReasons[userId] = reason;
                        DataBase.SauvegarderUtilisateurIndividuel(userId);
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"L'ID {userId} était déjà banni. Raison mise à jour : {reason}", cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"{userId} est déjà banni.", cancellationToken: cancellationToken);
                    }
                    return;
                }

                config.BanniUser.Add(msg[1]);
                if (!string.IsNullOrEmpty(reason))
                {
                    config.BanReasons[userId] = reason;
                }
                else
                {
                    config.BanReasons.Remove(userId);
                }

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
                DataBase.SauvegarderUtilisateurIndividuel(userId);

                string responseMsg = string.IsNullOrEmpty(reason)
                    ? $"L'ID {userId} a bien été banni."
                    : $"L'ID {userId} a bien été banni.\nRaison : {reason}";

                await botClient.SendTextMessageAsync(config.CurrentChatId, responseMsg, cancellationToken: cancellationToken);
                foreach (var id in config.idAdmins)
                {
                    await botClient.SendTextMessageAsync(id, $"BAN USER: {userId}\nRaison: {(string.IsNullOrEmpty(reason) ? "Aucune" : reason)}", cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex) { Console.WriteLine($"[Admin Erreur] {ex.Message}"); }
        }

        private static async Task DebanUser(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var msg = update.Message.Text.Split(' ');
                if (msg.Length != 2 || !long.TryParse(msg[1], out long userId))
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Erreur: Mauvais format ex: /deban <id>", cancellationToken: cancellationToken);
                    return;
                }

                if (config.BanniUser.Contains(msg[1]))
                {
                    config.BanniUser.Remove(msg[1]);
                }
                config.BanReasons.Remove(userId);

                int idx = config.UserSave.FindIndex(u => u.Item1 == userId);
                if (idx != -1)
                {
                    var old = config.UserSave[idx];
                    config.UserSave[idx] = Tuple.Create(old.Item1, old.Item2, old.Item3, false);
                }
                DataBase.SauvegarderUtilisateurIndividuel(userId);

                await botClient.SendTextMessageAsync(config.CurrentChatId, $"L'ID {msg[1]} a bien été débanni.", cancellationToken: cancellationToken);
                foreach (var id in config.idAdmins)
                {
                    await botClient.SendTextMessageAsync(id, $"DEBAN USER: {msg[1]}", cancellationToken: cancellationToken);
                }
            }
            catch { }
        }

        private static async Task GetInFoUser(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var msg = update.Message.Text.Split(' ');
                if (msg.Length == 2 && long.TryParse(msg[1], out long userId))
                {
                    int result = config.UserSave.FindIndex(tuple => tuple.Item1 == userId);
                    if (result == -1)
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"Erreur: {msg[1]} ID introuvable !!", cancellationToken: cancellationToken);
                        return;
                    }

                    var ancienTuple = config.UserSave[result];
                    string banReason = config.BanReasons.TryGetValue(userId, out var r) ? r : "";
                    string reasonLine = ancienTuple.Item4 ? $"\nRaison ban: {(string.IsNullOrEmpty(banReason) ? "Aucune spécifiée" : banReason)}" : "";

                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"Info de {msg[1]}\n\nAchat: {ancienTuple.Item2}\nSolde: {ancienTuple.Item3}€\nBanni: {ancienTuple.Item4}{reasonLine}", cancellationToken: cancellationToken);
                }
            }
            catch { }
        }

        private static async Task ConnaitreNombreDeStock(string message, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                string[] parts = message.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string brand = parts.Length > 1 ? parts[1].Trim().ToLower() : "carr";

                var connaitre = DataBase.ObtenirStocksParBrand(brand);
                if (connaitre.Count == 0)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"❌ Aucun stock disponible pour <b>{brand.ToUpper()}</b> !", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
                    return;
                }

                await botClient.SendTextMessageAsync(config.CurrentChatId, $"📦 Le stock pour <b>{brand.ToUpper()}</b> est de <b>{connaitre.Count}</b>", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
            }
            catch { }
        }

        private static async Task RecupererAchatId(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var message = update.Message.Text.Split(' ');
                if (message.Length != 2)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Erreur: /commandes <id | marque | code>", cancellationToken: cancellationToken);
                    return;
                }

                string searchArg = message[1];
                long.TryParse(searchArg, out long searchUserId);
                var transactions = DataBase.ObtenirTransactions();
                var sb = new StringBuilder();

                int compteur = 0;
                foreach (var tx in transactions)
                {
                    bool codeMatch = !string.IsNullOrEmpty(tx.Code) && tx.Code.Contains(searchArg, StringComparison.OrdinalIgnoreCase);
                    bool brandMatch = !string.IsNullOrEmpty(tx.Brand) && tx.Brand.Equals(searchArg, StringComparison.OrdinalIgnoreCase);
                    bool userMatch = tx.UserId == searchUserId && searchUserId > 0;

                    if (userMatch || codeMatch || brandMatch)
                    {
                        compteur++;
                        if (compteur > 20)
                        {
                            sb.AppendLine("... (Résultats tronqués, trop d'entrées)");
                            break;
                        }
                        DateTime dateParis = DataBase.ConvertirEnHeureParis(tx.CreatedAt);
                        sb.AppendLine($"Brand = {tx.Brand} | Carte = {tx.Code} | Solde = {tx.Value} | Prix = {tx.Price}€ | Date = {dateParis:dd/MM/yyyy HH:mm}");
                    }
                }

                if (sb.Length == 0)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"Aucune commande trouvée pour {searchArg}", cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"Historique d'achats pour {searchArg} :\n\n{sb}", cancellationToken: cancellationToken);
                }
            }
            catch { }
        }

        private static async Task HelpCommande(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var parts = update.Message.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 1)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("📋 *Commandes Administrateur* (par ordre d'importance) :\n");
                    foreach (var cmd in command)
                    {
                        sb.AppendLine($"• `{cmd}`");
                    }
                    sb.AppendLine("\n💡 _Tapez `/help <commande>` pour voir la description et l'exemple (ex: `/help stock`)._");
                    sb.AppendLine("💡 _Tapez `/help all` pour afficher le manuel complet._");

                    await botClient.SendTextMessageAsync(config.CurrentChatId, sb.ToString(), parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown, cancellationToken: cancellationToken);
                    return;
                }

                string arg = parts[1].Trim().TrimStart('/').ToLower();

                if (arg == "all")
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("📖 *Manuel Complet des Commandes Administrateur*\n");
                    foreach (var cmdKey in command)
                    {
                        string key = cmdKey.TrimStart('/').ToLower();
                        if (HelpDetails.TryGetValue(key, out var info))
                        {
                            sb.AppendLine($"🔹 *{cmdKey}*");
                            sb.AppendLine($"• *Description* : {info.description}");
                            sb.AppendLine($"• *Usage* : `{info.usage}`");
                            sb.AppendLine($"• *Exemple* : `{info.exemple}`\n");
                        }
                    }
                    await botClient.SendTextMessageAsync(config.CurrentChatId, sb.ToString(), parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown, cancellationToken: cancellationToken);
                    return;
                }

                if (HelpDetails.TryGetValue(arg, out var details))
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"ℹ️ *Aide pour la commande `/{arg}`*\n");
                    sb.AppendLine($"• *Description* : {details.description}");
                    sb.AppendLine($"• *Usage* : `{details.usage}`");
                    sb.AppendLine($"• *Exemple* : `{details.exemple}`");

                    await botClient.SendTextMessageAsync(config.CurrentChatId, sb.ToString(), parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown, cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"❌ Commande `/{arg}` inconnue. Tapez `/help` pour la liste des commandes.", parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown, cancellationToken: cancellationToken);
                }
            }
            catch { }
        }

        private static async Task GetInfoDePaiementId(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var id = update.Message.Text.Split(' ');
                if (id.Length != 2)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Erreur commandes: /crypto <trackId>", cancellationToken: cancellationToken);
                    return;
                }

                using var httpClient = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.oxapay.com/v1/payment/{id[1]}");
                request.Headers.Add("merchant_api_key", config.apiKey);

                config.IncOxaPaySent();
                var response = await httpClient.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"Transaction {id[1]} introuvable.", cancellationToken: cancellationToken);
                    return;
                }

                using JsonDocument doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var data))
                {
                    var status = data.GetProperty("status").GetString();
                    var montant = data.GetProperty("amount").GetDouble();

                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"Transaction {id[1]} [Montant={montant}€ | Status={status}]", cancellationToken: cancellationToken);
                }
            }
            catch { }
        }

        private static async Task ToggleMaintenance(string message, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                bool ancienState = config.ModeMaintenance;
                var parts = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    string arg = parts[1].ToLower();
                    if (arg == "on" || arg == "1" || arg == "true")
                    {
                        config.ModeMaintenance = true;
                    }
                    else if (arg == "off" || arg == "0" || arg == "false")
                    {
                        config.ModeMaintenance = false;
                    }
                    else
                    {
                        config.ModeMaintenance = !config.ModeMaintenance;
                    }
                }
                else
                {
                    config.ModeMaintenance = !config.ModeMaintenance;
                }

                if (ancienState != config.ModeMaintenance)
                {
                    _ = Program.AnnoncerModeMaintenance(botClient, config.ModeMaintenance, cancellationToken);
                }

                string statusText = config.ModeMaintenance ? "ACTIVÉ 🔴" : "DÉSACTIVÉ 🟢";
                await botClient.SendTextMessageAsync(config.CurrentChatId, $"🛠️ Mode Maintenance : <b>{statusText}</b>", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Admin Erreur] {ex.Message}");
            }
        }

        private static async Task SendPanelUrl(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                string domainEnv = Environment.GetEnvironmentVariable("RAILWAY_PUBLIC_DOMAIN") ?? Environment.GetEnvironmentVariable("RAILWAY_STATIC_URL") ?? "serveur-production-db21.up.railway.app";
                string slug = config.AdminSlug.Trim('/');
                string fullUrl = $"https://{domainEnv}/{slug}/";

                await botClient.SendTextMessageAsync(config.CurrentChatId, $"🔑 <b>URL d'Accès au Panel Admin Web :</b>\n\n<code>{fullUrl}</code>", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
            }
            catch { }
        }

        private static async Task AddStockFromFile(string message, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                string[] parts = message.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string brand = parts.Length > 1 ? parts[1].Trim().ToLower() : "carr";

                Document? doc = update.Message?.Document;
                if (doc == null && update.Message?.ReplyToMessage != null)
                {
                    doc = update.Message.ReplyToMessage.Document;
                }

                if (doc == null)
                {
                    await botClient.SendTextMessageAsync(
                        config.CurrentChatId,
                        "❌ <b>Erreur : Aucun fichier attaché !</b>\n\n" +
                        "Veuillez joindre un fichier <b>.txt</b> avec la légende <code>/addstock carr</code> (ou répondre à un fichier .txt avec cette commande).\n\n" +
                        "<b>Formats de ligne acceptés :</b>\n" +
                        "• <code>CODE:PIN:VALEUR:PRIX</code>\n" +
                        "• <code>CODE:PIN:VALEUR</code>\n" +
                        "• <code>CODE:PIN</code>\n" +
                        "• <code>CODE</code>\n\n" +
                        "<i>Séparateurs acceptés :</i> <code>:</code> <code>;</code> <code>|</code> <code>,</code>",
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        cancellationToken: cancellationToken
                    );
                    return;
                }

                string fileName = doc.FileName ?? "stock.txt";
                if (!fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) && doc.MimeType != "text/plain")
                {
                    await botClient.SendTextMessageAsync(
                        config.CurrentChatId,
                        "❌ <b>Format invalide !</b> Le fichier attaché doit obligatoirement être au format <b>.txt</b>.",
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        cancellationToken: cancellationToken
                    );
                    return;
                }

                var fileInfo = await botClient.GetFileAsync(doc.FileId, cancellationToken);
                using var memoryStream = new MemoryStream();
                await botClient.DownloadFileAsync(fileInfo.FilePath, memoryStream, cancellationToken);
                memoryStream.Position = 0;

                using var reader = new StreamReader(memoryStream, Encoding.UTF8);
                string? line;
                var stockItems = new List<DataBase.StockItem>();

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//")) continue;

                    string[] lineParts = trimmed.Split(new[] { ':', ';', '|', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lineParts.Length == 0) continue;

                    string code = lineParts[0].Trim();
                    string pin = lineParts.Length > 1 ? lineParts[1].Trim() : "";
                    string val = lineParts.Length > 2 ? lineParts[2].Trim() : "0";
                    string price = lineParts.Length > 3 ? lineParts[3].Trim() : "0";

                    if (!string.IsNullOrEmpty(code))
                    {
                        stockItems.Add(new DataBase.StockItem
                        {
                            Brand = brand,
                            Code = code,
                            Pin = pin,
                            Value = val,
                            Price = price
                        });
                    }
                }

                if (stockItems.Count == 0)
                {
                    await botClient.SendTextMessageAsync(
                        config.CurrentChatId,
                        "⚠️ <b>Aucune ligne de stock valide n'a été trouvée dans le fichier.</b>",
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        cancellationToken: cancellationToken
                    );
                    return;
                }

                int insertedCount = DataBase.InsererStockEnMasse(stockItems);

                await botClient.SendTextMessageAsync(
                    config.CurrentChatId,
                    $"✅ <b>Stock {brand.ToUpper()} Ajouté avec Succès !</b>\n\n" +
                    $"📄 <b>Fichier :</b> <code>{fileName}</code>\n" +
                    $"📦 <b>Cartes insérées en BDD :</b> <code>{insertedCount}</code>",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception ex)
            {
                await botClient.SendTextMessageAsync(
                    config.CurrentChatId,
                    $"❌ <b>Erreur lors de l'importation du stock :</b> {ex.Message}",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                    cancellationToken: cancellationToken
                );
            }
        }
    }
}
