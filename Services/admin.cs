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
            "/addmoney",
            "/removemoney",
            "/addstock",
            "/ban",
            "/deban",
            "/unlock",
            "/stat",
            "/stock",
            "/clear",
            "/commandes",
            "/info",
            "/crypto",
            "/message",
            "/maintenance",
            "/panel",
            "/bank",
            "/sumupbank",
            "/compteapi",
            "/comptepanel",
            "/demoiptv",
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
            { "clear", ("Vide intégralement le stock d'une marque (ex: /clear carr) ou la totalité du stock (/clear all).", "/clear carr", "/clear <marque|all>") },
            { "commandes", ("Recherche l'historique des achats par ID utilisateur, nom de marque ou code carte.", "/commandes 123456789", "/commandes <id|marque|code>") },
            { "info", ("Affiche les informations d'un utilisateur (solde, nombre d'achats, statut banni).", "/info 123456789", "/info <id>") },
            { "crypto", ("Interroge l'API OxaPay pour connaître le statut en direct d'une transaction.", "/crypto track_123456", "/crypto <trackId>") },
            { "message", ("Envoie un message de diffusion (broadcast) à tous les utilisateurs du bot.", "/message - Bonjour à tous !", "/message - <texte>") },
            { "maintenance", ("Active ou désactive le mode maintenance du bot.", "/maintenance on ou /maintenance off", "/maintenance [on|off]") },
            { "panel", ("Affiche l'URL secrète d'accès au Panel d'Administration Web.", "/panel", "/panel") },
            { "bank", ("Affiche ou modifie la banque active pour les paiements SumUp.", "/bank 1 ou /bank 2", "/bank [1|2]") },
            { "sumupbank", ("Affiche ou modifie la banque active pour les paiements SumUp.", "/sumupbank 1 ou /sumupbank 2", "/sumupbank [1|2]") },
            { "compteapi", ("Affiche ou change le compte API IPTV actif (abonnements payants 1/3/6/12 mois).", "/compteapi 2", "/compteapi [n°|nom]") },
            { "comptepanel", ("Affiche ou change le compte panel IPTV actif (user / mot de passe, pour les démos).", "/comptepanel 1", "/comptepanel [n°|nom]") },
            { "demoiptv", ("Active ou désactive le bouton d'achat démo IPTV dans le bot.", "/demoiptv on ou /demoiptv off", "/demoiptv [on|off]") },
            { "help", ("Affiche l'aide des commandes administration.", "/help ou /help stock ou /help all", "/help [commande|all]") }
        };

        public static async Task<bool> CommandeAdmin(string message, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (!config.idAdmins.Contains(config.CurrentChatId)) return false;
            if (string.IsNullOrWhiteSpace(message)) return false;

            string firstWord = message.Split(' ')[0].Trim();
            if (firstWord.Contains("@")) firstWord = firstWord.Split('@')[0].Trim();
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
                        await GetInfoUser(botClient, update, cancellationToken);
                        break;
                    case "/stock":
                        await ConnaitreNombreDeStock(message, botClient, update, cancellationToken);
                        break;
                    case "/clear":
                        await ClearStockCommand(message, botClient, update, cancellationToken);
                        break;
                    case "/commandes":
                        await RecupererAchatId(botClient, update, cancellationToken);
                        break;
                    case "/bank":
                    case "/sumupbank":
                        await BasculerBanqueSumUp(message, botClient, update, cancellationToken);
                        break;
                    case "/compteapi":
                        await SwitcherCompteApi(message, botClient, update, cancellationToken);
                        break;
                    case "/comptepanel":
                        await SwitcherComptePanel(message, botClient, update, cancellationToken);
                        break;
                    case "/demoiptv":
                        await ToggleDemoIptv(message, botClient, update, cancellationToken);
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
                    foreach (var item in config.CopierUtilisateurs())
                    {
                        try
                        {
                            await botClient.SendTextMessageAsync(item.Id, contenu, cancellationToken: cancellationToken);
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
                    if (config.EstEnCooldownPaiement(msg[1]))
                    {
                        config.RetirerCooldownPaiement(msg[1]);
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
                var msg = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (msg.Length == 3 && long.TryParse(msg[1], out long userId) && double.TryParse(msg[2].Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double mtn))
                {
                    double nouveauSolde = DataBase.CrediterSoldeAtomique(userId, mtn);

                    try
                    {
                        await botClient.SendTextMessageAsync(userId, $"💰 {mtn}€ reçus sur votre solde.", cancellationToken: cancellationToken);
                    }
                    catch { }

                    foreach (var idAdmin in config.idAdmins)
                    {
                        try
                        {
                            await botClient.SendTextMessageAsync(idAdmin, $"💰 <b>[ADMIN] Ajout de Solde</b>\n<b>User</b>: <code>{userId}</code>\n<b>Montant</b>: +{mtn}€\n<b>Nouveau Solde</b>: {nouveauSolde}€\n<b>Par</b>: @{config.CurrentPseudo} (<code>{config.CurrentChatId}</code>)", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
                        }
                        catch { }
                    }
                    return;
                }

                await botClient.SendTextMessageAsync(config.CurrentChatId, "Erreur: Mauvais format ex: /addMoney <id> <montant>", cancellationToken: cancellationToken);
            }
            catch (Exception ex) { Console.WriteLine($"[Admin Erreur] {ex.Message}"); }
        }

        private static async Task RemoveMoney(string message, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var msg = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (msg.Length == 3 && long.TryParse(msg[1], out long userId) && double.TryParse(msg[2].Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double mtn))
                {
                    DataBase.DebiterSoldeAtomique(userId, mtn, false, out double nouveauSolde);

                    try
                    {
                        await botClient.SendTextMessageAsync(userId, $"📉 {mtn}€ retirés de votre solde. Nouveau solde : {nouveauSolde}€.", cancellationToken: cancellationToken);
                    }
                    catch { }

                    foreach (var idAdmin in config.idAdmins)
                    {
                        try
                        {
                            await botClient.SendTextMessageAsync(idAdmin, $"📉 <b>[ADMIN] Retrait de Solde</b>\n<b>User</b>: <code>{userId}</code>\n<b>Montant</b>: -{mtn}€\n<b>Nouveau Solde</b>: {nouveauSolde}€\n<b>Par</b>: @{config.CurrentPseudo} (<code>{config.CurrentChatId}</code>)", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
                        }
                        catch { }
                    }
                    return;
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

                var uBan = config.ObtenirOuCreerUtilisateur(userId);
                uBan.IsBanned = true;
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

                var uDeban = config.ObtenirOuCreerUtilisateur(userId);
                uDeban.IsBanned = false;
                DataBase.SauvegarderUtilisateurIndividuel(userId);

                await botClient.SendTextMessageAsync(config.CurrentChatId, $"L'ID {msg[1]} a bien été débanni.", cancellationToken: cancellationToken);
                foreach (var id in config.idAdmins)
                {
                    await botClient.SendTextMessageAsync(id, $"DEBAN USER: {msg[1]}", cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex) { Console.WriteLine($"[Erreur Admin] {ex.Message}"); }
        }

        private static async Task GetInfoUser(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var msg = update.Message.Text.Split(' ');
                if (msg.Length == 2 && long.TryParse(msg[1], out long userId))
                {
                    var ancienTuple = config.TrouverUtilisateur(userId);
                    if (ancienTuple == null)
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"Erreur: {msg[1]} ID introuvable !!", cancellationToken: cancellationToken);
                        return;
                    }

                    string banReason = config.BanReasons.TryGetValue(userId, out var r) ? r : "";
                    string reasonLine = ancienTuple.IsBanned ? $"\nRaison ban: {(string.IsNullOrEmpty(banReason) ? "Aucune spécifiée" : banReason)}" : "";

                    await botClient.SendTextMessageAsync(config.CurrentChatId, $"Info de {msg[1]}\n\nAchat: {ancienTuple.Achat}\nSolde: {ancienTuple.Solde}€\nBanni: {ancienTuple.IsBanned}{reasonLine}", cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex) { Console.WriteLine($"[Erreur Admin] {ex.Message}"); }
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
            catch (Exception ex) { Console.WriteLine($"[Erreur Admin] {ex.Message}"); }
        }

        private static async Task RecupererAchatId(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var parts = update.Message.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                int days = 7; // Par défaut 7 jours pour un client/produit spécifique
                string searchArg = null;
                bool showAll = false;

                if (parts.Length == 1)
                {
                    // Ex: /commandes -> Global tous clients STRICTEMENT sur 1 JOUR (24h) MAX
                    showAll = true;
                    days = 1;
                }
                else if (parts.Length == 2)
                {
                    // Ex: /commandes 5883885733 OU /commandes carr
                    searchArg = parts[1].Trim();
                }
                else if (parts.Length >= 3)
                {
                    // Ex: /commandes 5883885733 14 OU /commandes carr 30
                    searchArg = parts[1].Trim();
                    if (int.TryParse(parts[2], out int d) && d > 0 && d <= 3650)
                    {
                        days = d;
                    }
                }

                long.TryParse(searchArg ?? "", out long searchUserId);
                DateTime cutoffDate = DateTime.UtcNow.AddDays(-days);

                var transactions = DataBase.ObtenirTransactions();

                var matchingTx = transactions.Where(tx =>
                {
                    bool timeMatch = tx.CreatedAt >= cutoffDate;
                    if (!timeMatch) return false;

                    if (showAll) return true;

                    bool codeMatch = !string.IsNullOrEmpty(tx.Code) && tx.Code.Contains(searchArg, StringComparison.OrdinalIgnoreCase);
                    bool brandMatch = !string.IsNullOrEmpty(tx.Brand) && tx.Brand.Equals(searchArg, StringComparison.OrdinalIgnoreCase);
                    bool userMatch = tx.UserId == searchUserId && searchUserId > 0;
                    return userMatch || codeMatch || brandMatch;
                }).ToList();

                string searchTitle = showAll ? "Global (Tous clients - 24h Max)" : searchArg;

                if (matchingTx.Count == 0)
                {
                    await botClient.SendTextMessageAsync(
                        config.CurrentChatId,
                        $"⚠️ <b>Aucune commande trouvée pour :</b> <code>{HtmlEncode(searchTitle)}</code> <i>(sur les {days} dernier{(days > 1 ? "s" : "")} jour{(days > 1 ? "s" : "")})</i>",
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        cancellationToken: cancellationToken
                    );
                    return;
                }

                var sb = new StringBuilder();
                var grouped = matchingTx.GroupBy(tx => string.IsNullOrWhiteSpace(tx.Brand) ? "PRODUIT" : tx.Brand.ToUpper());

                foreach (var group in grouped)
                {
                    sb.AppendLine($"📦 <b>{group.Key}</b> (<b>{group.Count()}</b>)");

                    foreach (var tx in group.Take(15))
                    {
                        DateTime dateParis = DataBase.ConvertirEnHeureParis(tx.CreatedAt);

                        if (tx.Brand?.Equals("iptv", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            string duree = int.TryParse(tx.Code, out int m) ? $"{m} mois" : tx.Code;
                            string iptvUsername = !string.IsNullOrWhiteSpace(tx.Pin) ? tx.Pin : "N/A";
                            sb.AppendLine($"• {HtmlEncode(duree)} | <code>{HtmlEncode(iptvUsername)}</code> | {tx.Price}€ | <i>{dateParis:dd/MM à HH:mm}</i>");
                        }
                        else if (!tx.Value.HasValue || tx.Value.Value <= 0)
                        {
                            string pinPart = string.IsNullOrWhiteSpace(tx.Pin) ? "" : $" | <b>PIN :</b> <code>{HtmlEncode(tx.Pin)}</code>";
                            sb.AppendLine($"• <code>{HtmlEncode(tx.Code)}</code>{pinPart} | {tx.Price}€ | <i>{dateParis:dd/MM à HH:mm}</i>");
                        }
                        else
                        {
                            string pinPart = string.IsNullOrWhiteSpace(tx.Pin) ? "" : $" | <b>PIN :</b> <code>{HtmlEncode(tx.Pin)}</code>";
                            sb.AppendLine($"• <code>{HtmlEncode(tx.Code)}</code>{pinPart} | {tx.Value.Value}€ ({tx.Price}€) | <i>{dateParis:dd/MM à HH:mm}</i>");
                        }
                    }

                    if (group.Count() > 15)
                    {
                        sb.AppendLine($"<i>... (+{group.Count() - 15} autres entrées)</i>");
                    }

                    sb.AppendLine();
                }

                string headerDays = days == 1 ? "Aujourd'hui (1 jour)" : $"{days} jour{(days > 1 ? "s" : "")}";
                await botClient.SendTextMessageAsync(
                    config.CurrentChatId,
                    $"📋 <b>Historique d'Achats :</b> <code>{HtmlEncode(searchTitle)}</code> (Total: <b>{matchingTx.Count}</b> | <b>{headerDays}</b>)\n\n{sb}",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RecupererAchatId Erreur] {ex.Message}");
            }
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
            catch (Exception ex) { Console.WriteLine($"[Erreur Admin] {ex.Message}"); }
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
            catch (Exception ex) { Console.WriteLine($"[Erreur Admin] {ex.Message}"); }
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
                string domainEnv = config.DomainePublic();
                string slug = config.AdminSlug.Trim('/');
                string fullUrl = $"https://{domainEnv}/{slug}/";

                await botClient.SendTextMessageAsync(config.CurrentChatId, $"🔑 <b>URL d'Accès au Panel Admin Web :</b>\n\n<code>{fullUrl}</code>", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
            }
            catch (Exception ex) { Console.WriteLine($"[Erreur Admin] {ex.Message}"); }
        }

        private static async Task AddStockFromFile(string message, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                string[] parts = message.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string rawBrand = parts.Length > 1 ? parts[1].Trim().ToLower() : "carr";

                string brand = rawBrand switch
                {
                    "carr" or "carrefour" => "carr",
                    "iptv" => "iptv",
                    _ => ""
                };

                if (string.IsNullOrEmpty(brand))
                {
                    await botClient.SendTextMessageAsync(
                        config.CurrentChatId,
                        $"❌ <b>Produit / Marque invalide !</b>\n\n" +
                        $"Le produit <code>{HtmlEncode(rawBrand)}</code> n'existe pas ou n'est pas reconnu par le système.\n\n" +
                        $"<b>Produits reconnus :</b>\n" +
                        $"• <code>carr</code> ou <code>carrefour</code> (Cartes Carrefour)\n" +
                        $"• <code>iptv</code> (Abonnements IPTV)\n\n" +
                        $"<i>Exemple :</i> <code>/addstock carr</code>",
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        cancellationToken: cancellationToken
                    );
                    return;
                }

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
                        "<b>Format obligatoire (4 champs séparés par <code>:</code>) :</b>\n" +
                        "<code>CODE:PIN:VALEUR:PRIX</code>\n\n" +
                        "<i>Exemple :</i> <code>2012345678901:1234:50:25</code>",
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
                        "❌ <b>Format invalide !</b> Le fichier attaché doit obligatoirement être un fichier texte au format <b>.txt</b>.",
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
                int lineNumber = 0;
                var stockItems = new List<DataBase.StockItem>();

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineNumber++;
                    string trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//")) continue;

                    string[] lineParts = trimmed.Split(':');
                    if (lineParts.Length != 4)
                    {
                        await botClient.SendTextMessageAsync(
                            config.CurrentChatId,
                            $"❌ <b>Erreur de format de ligne !</b>\n\n" +
                            $"📍 <b>Ligne {lineNumber} :</b> <code>{HtmlEncode(trimmed)}</code>\n\n" +
                            $"⚠️ <b>Constat :</b> La ligne contient <b>{lineParts.Length} champ(s)</b> au lieu des <b>4 champs obligatoires</b> séparés par des deux-points <code>:</code>.\n\n" +
                            $"💡 <b>Format obligatoire :</b> <code>CODE:PIN:VALEUR:PRIX</code>",
                            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                            cancellationToken: cancellationToken
                        );
                        return;
                    }

                    string code = lineParts[0].Trim();
                    string pin = lineParts[1].Trim();
                    string val = lineParts[2].Trim();
                    string price = lineParts[3].Trim();

                    if (string.IsNullOrEmpty(code))
                    {
                        await botClient.SendTextMessageAsync(
                            config.CurrentChatId,
                            $"❌ <b>Erreur de format de ligne !</b>\n\n" +
                            $"📍 <b>Ligne {lineNumber} :</b> <code>{HtmlEncode(trimmed)}</code>\n\n" +
                            $"⚠️ <b>Constat :</b> Le champ <b>CODE</b> est vide !",
                            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                            cancellationToken: cancellationToken
                        );
                        return;
                    }

                    stockItems.Add(new DataBase.StockItem
                    {
                        Brand = brand,
                        Code = code,
                        Pin = pin,
                        Value = val,
                        Price = price
                    });
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

        private static async Task ClearStockCommand(string message, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                string[] parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    await botClient.SendTextMessageAsync(
                        config.CurrentChatId,
                        "⚠️ <b>Utilisation :</b> <code>/clear &lt;marque|all&gt;</code>\n\n" +
                        "<i>Exemples :</i>\n" +
                        "• <code>/clear carr</code> (Vide le stock Carrefour)\n" +
                        "• <code>/clear all</code> (Vide l'intégralité du stock)",
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        cancellationToken: cancellationToken
                    );
                    return;
                }

                string targetBrand = parts[1].Trim().ToLower();
                int deletedCount = DataBase.ViderStockBDD(targetBrand);

                if (deletedCount > 0)
                {
                    await botClient.SendTextMessageAsync(
                        config.CurrentChatId,
                        $"🗑️ <b>Stock vidé avec succès !</b>\n\n" +
                        $"<b>Marque :</b> <code>{targetBrand.ToUpper()}</code>\n" +
                        $"<b>Cartes supprimées :</b> <code>{deletedCount}</code>",
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        cancellationToken: cancellationToken
                    );
                }
                else
                {
                    await botClient.SendTextMessageAsync(
                        config.CurrentChatId,
                        $"ℹ️ Aucun stock disponible à supprimer pour la marque <code>{targetBrand.ToUpper()}</code>.",
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        cancellationToken: cancellationToken
                    );
                }
            }
            catch (Exception ex)
            {
                await botClient.SendTextMessageAsync(
                    config.CurrentChatId,
                    $"❌ <b>Erreur /clear :</b> {ex.Message}",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                    cancellationToken: cancellationToken
                );
            }
        }

        public static async Task BasculerBanqueSumUp(string message, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            string[] parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string name1 = config.ExigerSumUp("sumup", "name");
            string email1 = config.ExigerSumUp("sumup", "pay_to_email");
            string name2 = config.ExigerSumUp("sumup_bank2", "name");
            string email2 = config.ExigerSumUp("sumup_bank2", "pay_to_email");
            string activeCat = config.SumUpActiveCategory;
            string bankName = activeCat == "sumup_bank2" ? name2 : name1;
            string email = activeCat == "sumup_bank2" ? email2 : email1;

            if (parts.Length < 2)
            {
                string txt = $"🏦 <b>BANQUE SUMUP ACTUELLE</b>\n\n" +
                             $"• Banque active : <b>{bankName}</b>\n" +
                             $"• E-mail associé : <code>{email}</code>\n\n" +
                             $"<b>Changer de banque :</b>\n" +
                             $"• <code>/bank 1</code> : {name1}\n" +
                             $"• <code>/bank 2</code> : {name2}";
                await botClient.SendTextMessageAsync(config.CurrentChatId, txt, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
                return;
            }

            string choice = parts[1].Trim().ToLower();
            if (choice == "1" || choice == "bank1" || choice == "sumup")
            {
                config.SumUpActiveBank = "sumup";
                await botClient.SendTextMessageAsync(config.CurrentChatId, $"✅ <b>Banque SumUp modifiée !</b>\n\nCompte actif : <b>{name1}</b>\nE-mail : <code>{email1}</code>", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
            }
            else if (choice == "2" || choice == "bank2" || choice == "sumup_bank2")
            {
                config.SumUpActiveBank = "sumup_bank2";
                await botClient.SendTextMessageAsync(config.CurrentChatId, $"✅ <b>Banque SumUp modifiée !</b>\n\nCompte actif : <b>{name2}</b>\nE-mail : <code>{email2}</code>", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendTextMessageAsync(config.CurrentChatId, "❌ Choix invalide. Utilisez <code>/bank 1</code> ou <code>/bank 2</code>.", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
            }
        }

        private static async Task SwitcherCompteApi(string message, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var list = iptv.GetAccounts();
            string[] parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                var sb = new StringBuilder();
                sb.AppendLine("📡 <b>COMPTES API IPTV</b>\n");
                if (list.Count == 0)
                {
                    sb.AppendLine("Aucun compte configuré.");
                }
                else
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var a = list[i];
                        string nom = string.IsNullOrWhiteSpace(a.Name) ? $"Compte {i + 1}" : HtmlEncode(a.Name);
                        string pack = HtmlEncode(a.Pack);
                        string actif = a.Active ? " — <b>ACTIF</b>" : "";
                        sb.AppendLine($"{i + 1}. {nom} | pack <code>{pack}</code>{actif}");
                    }
                    sb.AppendLine("\nChanger : <code>/compteapi 1</code> ou <code>/compteapi Nom</code>");
                }
                await botClient.SendTextMessageAsync(config.CurrentChatId, sb.ToString(), parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
                return;
            }

            if (!iptv.ActiverCompte(parts[1], out string label, out string erreur))
            {
                await botClient.SendTextMessageAsync(config.CurrentChatId, "❌ " + HtmlEncode(erreur), parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
                return;
            }

            await botClient.SendTextMessageAsync(config.CurrentChatId, $"✅ Compte API actif : <b>{HtmlEncode(label)}</b>", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
        }

        private static async Task SwitcherComptePanel(string message, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var list = IptvPanel.GetPanelAccounts();
            string[] parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                var sb = new StringBuilder();
                sb.AppendLine("🖥 <b>COMPTES PANEL IPTV</b>\n");
                if (list.Count == 0)
                {
                    sb.AppendLine("Aucun compte configuré.");
                }
                else
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var a = list[i];
                        string nom = string.IsNullOrWhiteSpace(a.Name) ? a.Username : a.Name;
                        if (string.IsNullOrWhiteSpace(nom)) nom = $"Compte {i + 1}";
                        string user = HtmlEncode(a.Username);
                        string actif = a.Active ? " — <b>ACTIF</b>" : "";
                        sb.AppendLine($"{i + 1}. {HtmlEncode(nom)} | user <code>{user}</code>{actif}");
                    }
                    sb.AppendLine("\nChanger : <code>/comptepanel 1</code> ou <code>/comptepanel Nom</code>");
                }
                await botClient.SendTextMessageAsync(config.CurrentChatId, sb.ToString(), parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
                return;
            }

            if (!IptvPanel.ActiverCompte(parts[1], out string label, out string erreur))
            {
                await botClient.SendTextMessageAsync(config.CurrentChatId, "❌ " + HtmlEncode(erreur), parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
                return;
            }

            await botClient.SendTextMessageAsync(config.CurrentChatId, $"✅ Compte panel actif : <b>{HtmlEncode(label)}</b>", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
        }

        private static async Task ToggleDemoIptv(string message, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            string[] parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                string etat = iptv.DemoEnabled ? "ACTIVÉES 🟢" : "DÉSACTIVÉES 🔴";
                string txt = $"📺 <b>ACHATS DÉMO IPTV</b>\n\n" +
                             $"État : <b>{etat}</b>\nPrix : <b>{iptv.PrixDemo.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}€</b>\n\n" +
                             $"• <code>/demoiptv on</code> : afficher le bouton Démo et autoriser l'achat\n" +
                             $"• <code>/demoiptv off</code> : cacher le bouton et bloquer l'achat";
                await botClient.SendTextMessageAsync(config.CurrentChatId, txt, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
                return;
            }

            string arg = parts[1].Trim().ToLower();
            if (arg == "on" || arg == "1" || arg == "true" || arg == "enable")
                iptv.DemoEnabled = true;
            else if (arg == "off" || arg == "0" || arg == "false" || arg == "disable")
                iptv.DemoEnabled = false;
            else
            {
                await botClient.SendTextMessageAsync(config.CurrentChatId, "❌ Utilise <code>/demoiptv on</code> ou <code>/demoiptv off</code>.", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
                return;
            }

            string statusText = iptv.DemoEnabled ? "ACTIVÉES 🟢" : "DÉSACTIVÉES 🔴";
            await botClient.SendTextMessageAsync(config.CurrentChatId, $"📺 Achats démo IPTV : <b>{statusText}</b>", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
        }

        private static string HtmlEncode(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
