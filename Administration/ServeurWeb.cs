using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Telegram.Bot;

namespace ChezRheyyBot
{
    internal class ServeurWeb
    {
        private static string _adminSecretToken => config.GetSetting("admin", "password", "");
        private static readonly ConcurrentDictionary<string, DateTime> _adminSessions = new();
        private static readonly ConcurrentDictionary<string, (int count, DateTime window)> _loginAttempts = new();

        public static async Task LancerServeurWebAdmin(ITelegramBotClient botClient, CancellationToken cancellationToken)
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
                listener.Prefixes.Add($"http://*:{port}/");
                listener.Start();
                Console.WriteLine($"[Serveur Web Admin] Démarré sur le port {port}.");
            }
            catch
            {
                try
                {
                    listener.Prefixes.Clear();
                    listener.Prefixes.Add($"http://+:{port}/");
                    listener.Start();
                    Console.WriteLine($"[Serveur Web Admin] Démarré sur le port {port} (fallback +).");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Serveur Web Admin Erreur] Impossible d'écouter sur le port {port}: {ex.Message}");
                    return;
                }
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    HttpListenerContext context = await listener.GetContextAsync();
                    _ = Task.Run(() => TraiterRequete(context, botClient, cancellationToken), cancellationToken);
                }
                catch (Exception ex)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    Console.WriteLine($"[Serveur Web Admin Exception] {ex.Message}");
                }
            }
        }

        private static async Task TraiterRequete(HttpListenerContext context, ITelegramBotClient botClient, CancellationToken cancellationToken)
        {
            var request = context.Request;
            var response = context.Response;
            string rawUrl = request.RawUrl ?? "/";

            try
            {
                if (rawUrl.StartsWith("/webhook/telegram"))
                {
                    string providedSecret = request.Headers["X-Telegram-Bot-Api-Secret-Token"] ?? "";
                    if (!SecretsEgaux(providedSecret, config.TelegramWebhookSecret))
                    {
                        RepondreJson(response, 401, new { ok = false });
                        return;
                    }

                    using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                    string bodyStr = await reader.ReadToEndAsync();
                    if (!string.IsNullOrWhiteSpace(bodyStr))
                    {
                        try
                        {
                            var update = Newtonsoft.Json.JsonConvert.DeserializeObject<Telegram.Bot.Types.Update>(bodyStr);
                            if (update != null)
                            {
                                _ = Task.Run(() => Program.TraiterUpdateWebhook(botClient, update, cancellationToken), cancellationToken);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Webhook Deserialize Error] {ex.Message}");
                        }
                    }
                    RepondreJson(response, 200, new { ok = true });
                    return;
                }

                if (rawUrl.StartsWith("/webhook/sumup/"))
                {
                    await paiement.TraiterRequeteWebhookPublique(context, botClient, cancellationToken);
                    return;
                }

                if (rawUrl.StartsWith("/api/admin"))
                {
                    await GererRequeteApi(context, botClient, cancellationToken);
                    return;
                }

                await GererFichiersStatiques(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Serveur Web Admin Exception Requete] {ex.Message}");
                RepondreJson(response, 500, new { success = false, message = "Erreur serveur interne" });
            }
        }

        private static async Task GererRequeteApi(HttpListenerContext context, ITelegramBotClient botClient, CancellationToken cancellationToken)
        {
            var request = context.Request;
            var response = context.Response;
            string path = request.Url?.AbsolutePath.ToLower() ?? "";

            if (path == "/api/admin/login" && request.HttpMethod == "POST")
            {
                string ip = request.RemoteEndPoint?.Address?.ToString() ?? "unknown";
                if (!AutoriserTentativeLogin(ip))
                {
                    RepondreJson(response, 429, new { success = false, message = "Trop de tentatives" });
                    return;
                }

                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);
                string pwd = doc.RootElement.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "";

                if (VerifierMotDePasseAdmin(pwd))
                {
                    config.IncAdminLogins();
                    string session = CreerSessionAdmin();
                    RepondreJson(response, 200, new { success = true, token = session });
                }
                else
                {
                    RepondreJson(response, 401, new { success = false, message = "Mot de passe incorrect" });
                }
                return;
            }

            string authHeader = request.Headers["Authorization"] ?? "";
            string tokenProvided = authHeader.StartsWith("Bearer ") ? authHeader.Substring(7).Trim() : "";

            if (!SessionAdminValide(tokenProvided))
            {
                RepondreJson(response, 401, new { success = false, message = "Non autorisé" });
                return;
            }

            if (path == "/api/admin/stats" && request.HttpMethod == "GET")
            {
                DataBase.SauvegarderSettings();
                var transactions = DataBase.ObtenirTransactions();
                var allPayments = DataBase.ObtenirTousLesPaiementsBDD();
                double totalRecharges = allPayments.Where(p => string.Equals(p.Status, "PAID", StringComparison.OrdinalIgnoreCase)).Sum(p => p.Amount);
                double totalVentes = transactions.Sum(t => t.Price);
                double totalCa = totalRecharges > 0 ? totalRecharges : totalVentes;
                int totalSales = transactions.Count;
                int totalUsers = config.CopierUtilisateurs().Count;
                var stock = DataBase.ObtenirStocksParBrand("carr");
                int totalStock = stock.Count;

                var recentSales = transactions.OrderByDescending(x => x.CreatedAt).Take(10).Select(t => new
                {
                    id = t.Id,
                    userId = t.UserId,
                    brand = t.Brand,
                    price = t.Price,
                    createdAt = DataBase.ConvertirEnHeureParis(t.CreatedAt).ToString("yyyy-MM-ddTHH:mm:ss")
                }).ToList();

                var recentPayments = allPayments.OrderByDescending(x => x.CreatedAt).Take(10).Select(p => new
                {
                    id = p.Id,
                    chatId = p.ChatId,
                    trackId = p.TrackId,
                    amount = p.Amount,
                    method = p.PaymentMethod,
                    status = p.Status,
                    createdAt = DataBase.ConvertirEnHeureParis(p.CreatedAt).ToString("yyyy-MM-ddTHH:mm:ss")
                }).ToList();

                var metrics = new
                {
                    telegramReceived = config.MetricTelegramReceived,
                    telegramSent = config.MetricTelegramSent,
                    sumupReceived = config.MetricSumUpReceived,
                    sumupSent = config.MetricSumUpSent,
                    oxapayReceived = config.MetricOxaPayReceived,
                    oxapaySent = config.MetricOxaPaySent,
                    commandsExecuted = config.MetricCommandsExecuted,
                    errorsCount = config.MetricErrorsCount,
                    adminLogins = config.MetricAdminLogins
                };

                var evenements = transactions.Select(t => DataBase.ConvertirEnHeureParis(t.CreatedAt))
                    .Concat(allPayments.Select(p => DataBase.ConvertirEnHeureParis(p.CreatedAt)))
                    .Where(d => d != DateTime.MinValue)
                    .ToList();

                var nowParis = DataBase.ConvertirEnHeureParis(DateTime.UtcNow);
                var todayStart = nowParis.Date;
                var todayBlocks = new List<object>();
                for (int i = 11; i >= 0; i--)
                {
                    var blockTime = nowParis.AddHours(-i * 2);
                    int hStart = (blockTime.Hour / 2) * 2;
                    int hEnd = hStart + 2;
                    string lbl = $"{hStart:D2}h-{hEnd:D2}h";
                    long blockVol = evenements.Count(d => d.Date == todayStart && d.Hour >= hStart && d.Hour < hEnd);
                    todayBlocks.Add(new { label = lbl, volume = blockVol });
                }

                var last7Days = new List<object>();
                for (int i = 6; i >= 0; i--)
                {
                    var dayDate = todayStart.AddDays(-i);
                    long dayVol = evenements.Count(d => d.Date == dayDate);
                    last7Days.Add(new { label = dayDate.ToString("dd/MM"), volume = dayVol });
                }

                var last30Days = new List<object>();
                for (int i = 29; i >= 0; i--)
                {
                    var dayDate = todayStart.AddDays(-i);
                    long dayVol = evenements.Count(d => d.Date == dayDate);
                    last30Days.Add(new { label = dayDate.ToString("dd/MM"), volume = dayVol });
                }

                string startStr = request.QueryString["startDate"] ?? "";
                string endStr = request.QueryString["endDate"] ?? "";
                var customBlocks = new List<object>();
                if (!string.IsNullOrEmpty(startStr) && !string.IsNullOrEmpty(endStr) && DateTime.TryParse(startStr, out DateTime sDate) && DateTime.TryParse(endStr, out DateTime eDate))
                {
                    var cur = sDate.Date;
                    var limit = eDate.Date;
                    if (limit < cur) { var temp = cur; cur = limit; limit = temp; }
                    int daySpan = (limit - cur).Days;
                    if (daySpan > 90) daySpan = 90;
                    for (int i = 0; i <= daySpan; i++)
                    {
                        var dayDate = cur.AddDays(i);
                        long dayVol = evenements.Count(d => d.Date == dayDate);
                        customBlocks.Add(new { label = dayDate.ToString("dd/MM"), volume = dayVol });
                    }
                }

                var history = new
                {
                    today = todayBlocks,
                    days7 = last7Days,
                    days30 = last30Days,
                    custom = customBlocks
                };

                bool maintenance = config.ModeMaintenance;
                RepondreJson(response, 200, new { totalCa, totalRecharges, totalVentes, totalSales, totalUsers, totalStock, maintenance, recentSales, recentPayments, metrics, history });
            }
            else if (path == "/api/admin/metrics/reset" && request.HttpMethod == "POST")
            {
                lock (config.SettingsLock)
                {
                    config.MetricTelegramReceived = 0;
                    config.MetricTelegramSent = 0;
                    config.MetricSumUpReceived = 0;
                    config.MetricSumUpSent = 0;
                    config.MetricOxaPayReceived = 0;
                    config.MetricOxaPaySent = 0;
                    config.MetricCommandsExecuted = 0;
                    config.MetricErrorsCount = 0;
                    config.MetricAdminLogins = 0;
                    config.PersisterMetricsInSettings();
                }
                DataBase.SauvegarderSettings();
                RepondreJson(response, 200, new { success = true, message = "Compteurs réinitialisés avec succès" });
            }
            else if (path == "/api/admin/payments" && request.HttpMethod == "GET")
            {
                var payments = DataBase.ObtenirTousLesPaiementsBDD().Select(p => new
                {
                    id = p.Id,
                    chatId = p.ChatId,
                    trackId = p.TrackId,
                    amount = p.Amount,
                    method = p.PaymentMethod,
                    status = p.Status,
                    url = p.PaymentUrl,
                    createdAt = DataBase.ConvertirEnHeureParis(p.CreatedAt).ToString("yyyy-MM-ddTHH:mm:ss")
                }).ToList();

                RepondreJson(response, 200, new { payments });
            }
            else if (path == "/api/admin/maintenance" && request.HttpMethod == "GET")
            {
                RepondreJson(response, 200, new { maintenance = config.ModeMaintenance });
            }
            else if (path == "/api/admin/maintenance" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);
                bool mtn = doc.RootElement.GetProperty("maintenance").GetBoolean();
                if (config.ModeMaintenance != mtn)
                {
                    config.ModeMaintenance = mtn;
                    _ = Program.AnnoncerModeMaintenance(botClient, mtn, cancellationToken);
                }
                RepondreJson(response, 200, new { success = true, maintenance = config.ModeMaintenance });
            }
            else if (path == "/api/admin/users" && request.HttpMethod == "GET")
            {
                var users = config.CopierUtilisateurs().Select(u => new
                {
                    userNumber = config.ObtenirOuCreerNumeroUtilisateur(u.Id),
                    id = u.Id,
                    username = config.ObtenirUsername(u.Id),
                    achats = u.Achat,
                    solde = u.Solde,
                    isBanned = u.IsBanned,
                    isAdmin = config.idAdmins.Contains(u.Id.ToString()),
                    banReason = config.BanReasons.TryGetValue(u.Id, out var r) ? r : ""
                }).OrderBy(u => u.userNumber).ToList();

                RepondreJson(response, 200, new { users });
            }
            else if (path == "/api/admin/users/solde" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);
                var root = doc.RootElement;

                long userId = 0;
                if (root.TryGetProperty("userId", out var uElem))
                {
                    long.TryParse(GetJsonStringOrNumber(uElem), out userId);
                }

                string action = root.TryGetProperty("action", out var actElem) ? actElem.GetString() ?? "add" : "add";

                double amount = 0;
                if (root.TryGetProperty("amount", out var amtElem))
                {
                    double.TryParse(GetJsonStringOrNumber(amtElem).Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out amount);
                }

                if (userId > 0)
                {
                    double newSolde;
                    if (action.Equals("add", StringComparison.OrdinalIgnoreCase))
                    {
                        newSolde = DataBase.CrediterSoldeAtomique(userId, amount);
                    }
                    else if (action.Equals("remove", StringComparison.OrdinalIgnoreCase) || action.Equals("sub", StringComparison.OrdinalIgnoreCase))
                    {
                        DataBase.DebiterSoldeAtomique(userId, amount, false, out newSolde);
                    }
                    else
                    {
                        var current = config.ObtenirOuCreerUtilisateur(userId);
                        double delta = amount - current.Solde;
                        if (delta >= 0) newSolde = DataBase.CrediterSoldeAtomique(userId, delta);
                        else
                        {
                            DataBase.DebiterSoldeAtomique(userId, -delta, false, out newSolde);
                        }
                    }

                    foreach (var idAdmin in config.idAdmins)
                    {
                        try
                        {
                            await botClient.SendTextMessageAsync(idAdmin, $"💳 <b>[PANEL WEB ADMIN] Modification Solde</b>\n<b>User</b>: <code>{userId}</code>\n<b>Action</b>: {action} ({amount}€)\n<b>Nouveau Solde</b>: {newSolde}€", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
                        }
                        catch { }
                    }

                    RepondreJson(response, 200, new { success = true, newSolde });
                }
                else
                {
                    RepondreJson(response, 400, new { success = false, message = "ID utilisateur invalide" });
                }
            }
            else if (path == "/api/admin/users/ban" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);
                var root = doc.RootElement;

                long userId = long.Parse(GetJsonStringOrNumber(root.GetProperty("userId")));
                bool ban = root.GetProperty("ban").GetBoolean();
                string reason = root.TryGetProperty("reason", out var rElem) ? rElem.GetString() ?? "" : "";

                if (ban)
                {
                    if (!config.BanniUser.Contains(userId.ToString())) config.BanniUser.Add(userId.ToString());
                    if (!string.IsNullOrEmpty(reason)) config.BanReasons[userId] = reason;
                    else config.BanReasons.Remove(userId);
                }
                else
                {
                    config.BanniUser.Remove(userId.ToString());
                    config.BanReasons.Remove(userId);
                }

                var uBan = config.ObtenirOuCreerUtilisateur(userId);
                uBan.IsBanned = ban;
                DataBase.SauvegarderUtilisateurIndividuel(userId);

                foreach (var idAdmin in config.idAdmins)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(idAdmin, $"🚫 <b>[PANEL WEB ADMIN] Statut Utilisateur</b>\n<b>User</b>: <code>{userId}</code>\n<b>Statut</b>: {(ban ? "BAN 🚫" : "DEBAN 🔓")}\n<b>Raison</b>: {(string.IsNullOrEmpty(reason) ? "Aucune" : reason)}", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
                    }
                    catch { }
                }

                RepondreJson(response, 200, new { success = true });
            }
            else if (path == "/api/admin/users/delete" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);

                long userId = long.Parse(GetJsonStringOrNumber(doc.RootElement.GetProperty("userId")));
                bool deleted = DataBase.SupprimerUtilisateurCompletBDD(userId);

                if (deleted)
                {
                    foreach (var idAdmin in config.idAdmins)
                    {
                        try
                        {
                            await botClient.SendTextMessageAsync(idAdmin, $"🗑️ <b>[PANEL WEB ADMIN] Suppression Utilisateur</b>\n<b>User</b>: <code>{userId}</code>", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: cancellationToken);
                        }
                        catch { }
                    }
                }

                RepondreJson(response, 200, new { success = deleted });
            }
            else if (path == "/api/admin/users/sync-user" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);
                var root = doc.RootElement;

                long userId = 0;
                if (root.TryGetProperty("userId", out var uElem))
                {
                    long.TryParse(GetJsonStringOrNumber(uElem), out userId);
                }

                if (userId > 0)
                {
                    string username = "";
                    string statusMsg = "";
                    try
                    {
                        var chat = await botClient.GetChatAsync(new Telegram.Bot.Types.ChatId(userId));
                        if (chat != null)
                        {
                            if (!string.IsNullOrWhiteSpace(chat.Username))
                            {
                                username = chat.Username.StartsWith("@") ? chat.Username : "@" + chat.Username;
                                statusMsg = "Pseudo trouvé";
                            }
                            else
                            {
                                string fullName = $"{chat.FirstName} {chat.LastName}".Trim();
                                if (!string.IsNullOrWhiteSpace(fullName))
                                {
                                    username = fullName;
                                    statusMsg = "Nom sans @ trouvé";
                                }
                                else
                                {
                                    username = "N/A";
                                    statusMsg = "Aucun pseudo configuré";
                                }
                            }

                            config.Usernames[userId] = username;
                            DataBase.SauvegarderUtilisateurIndividuel(userId);
                        }
                        else
                        {
                            statusMsg = "Chat non accessible";
                        }
                    }
                    catch (Exception ex)
                    {
                        statusMsg = $"Non accessible (Bot non démarré) : {ex.Message}";
                    }

                    RepondreJson(response, 200, new { success = true, userId, username, message = statusMsg });
                }
                else
                {
                    RepondreJson(response, 400, new { success = false, message = "ID utilisateur invalide" });
                }
            }
            else if (path == "/api/admin/users/sync-usernames" && request.HttpMethod == "POST")
            {
                await DataBase.SynchroniserUsernamesTelegram(botClient);
                RepondreJson(response, 200, new { success = true });
            }
            else if (path == "/api/admin/stock" && request.HttpMethod == "GET")
            {
                var stock = DataBase.ObtenirStocksParBrand("carr").Select(s => new
                {
                    id = s.Id,
                    code = s.Code,
                    pin = s.Pin,
                    value = s.Value,
                    price = s.Price
                }).ToList();

                RepondreJson(response, 200, new { stock });
            }
            else if (path == "/api/admin/stock/add" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);
                var root = doc.RootElement;

                if (root.TryGetProperty("items", out var itemsElem) && itemsElem.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<DataBase.StockItem>();
                    foreach (var elem in itemsElem.EnumerateArray())
                    {
                        string b = elem.TryGetProperty("brand", out var bE) ? bE.GetString() ?? "carr" : "carr";
                        string c = elem.TryGetProperty("code", out var cE) ? cE.GetString() ?? "" : "";
                        string p = elem.TryGetProperty("pin", out var pE) ? pE.GetString() ?? "" : "";
                        int.TryParse(elem.TryGetProperty("value", out var vE) ? GetJsonStringOrNumber(vE) : "0", out int v);
                        double.TryParse(elem.TryGetProperty("price", out var prE) ? GetJsonStringOrNumber(prE).Replace(',', '.') : "0", System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double pr);

                        if (!string.IsNullOrWhiteSpace(c))
                        {
                            list.Add(new DataBase.StockItem
                            {
                                Brand = b,
                                Code = c,
                                Pin = p,
                                Value = v.ToString(),
                                Price = pr.ToString()
                            });
                        }
                    }

                    int count = DataBase.InsererStockEnMasse(list);
                    RepondreJson(response, 200, new { success = true, count });
                }
                else
                {
                    string brand = root.GetProperty("brand").GetString() ?? "carr";
                    string code = root.GetProperty("code").GetString() ?? "";
                    string pin = root.TryGetProperty("pin", out var pElem) ? pElem.GetString() ?? "" : "";
                    int value = int.Parse(GetJsonStringOrNumber(root.GetProperty("value")));
                    double price = double.Parse(GetJsonStringOrNumber(root.GetProperty("price")).Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);

                    DataBase.InsererDansStock(brand, code, pin, value, price);
                    RepondreJson(response, 200, new { success = true, count = 1 });
                }
            }
            else if (path == "/api/admin/stock/delete" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);
                int id = int.Parse(GetJsonStringOrNumber(doc.RootElement.GetProperty("id")));

                bool deleted = DataBase.SupprimerStockParId(id);
                RepondreJson(response, 200, new { success = deleted });
            }
            else if (path == "/api/admin/stock/clear" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);
                string brand = doc.RootElement.TryGetProperty("brand", out var bElem) ? bElem.GetString() ?? "carr" : "carr";

                int deletedCount = DataBase.ViderStockBDD(brand);
                RepondreJson(response, 200, new { success = true, count = deletedCount });
            }
            else if (path == "/api/admin/transactions" && request.HttpMethod == "GET")
            {
                var transactions = DataBase.ObtenirTransactions().Select(t => new
                {
                    id = t.Id,
                    userId = t.UserId,
                    brand = t.Brand,
                    code = t.Code,
                    value = t.Value,
                    price = t.Price,
                    createdAt = DataBase.ConvertirEnHeureParis(t.CreatedAt).ToString("yyyy-MM-ddTHH:mm:ss")
                }).ToList();

                RepondreJson(response, 200, new { transactions });
            }
            else if (path == "/api/admin/settings" && request.HttpMethod == "GET")
            {
                var iptvDict = config.CategorySettings.TryGetValue("iptv", out var dict) ? dict : new Dictionary<string, string>();
                var iptv = new
                {
                    host = iptvDict.TryGetValue("host", out var h) ? h : "",
                    type = iptvDict.TryGetValue("type", out var t) ? t : "",
                    message_footer = iptvDict.TryGetValue("message_footer", out var f) ? f : "",
                    price_1m = iptvDict.TryGetValue("price_1m", out var p1v) ? p1v : "",
                    price_3m = iptvDict.TryGetValue("price_3m", out var p3v) ? p3v : "",
                    price_6m = iptvDict.TryGetValue("price_6m", out var p6v) ? p6v : "",
                    price_12m = iptvDict.TryGetValue("price_12m", out var p12v) ? p12v : "",
                    accounts = ChezRheyyBot.iptv.GetAccounts(),
                    panel_accounts = IptvPanel.GetPanelAccounts()
                };
                string LireCat(string cat, string key)
                {
                    if (config.CategorySettings.TryGetValue(cat, out var d) && d != null && d.TryGetValue(key, out var v))
                        return v ?? "";
                    return "";
                }
                string telegramMode = LireCat("general", "telegram_mode");
                string sumupMode = LireCat("general", "sumup_mode");
                var sumup = new
                {
                    active = LireCat("general", "sumup_active_bank"),
                    expiration_minutes = LireCat("general", "sumup_expiration_minutes"),
                    banks = new
                    {
                        sumup = new
                        {
                            name = LireCat("sumup", "name"),
                            pay_to_email = LireCat("sumup", "pay_to_email"),
                            api_key = LireCat("sumup", "api_key"),
                            client_id = LireCat("sumup", "client_id"),
                            client_secret = LireCat("sumup", "client_secret")
                        },
                        sumup_bank2 = new
                        {
                            name = LireCat("sumup_bank2", "name"),
                            pay_to_email = LireCat("sumup_bank2", "pay_to_email"),
                            api_key = LireCat("sumup_bank2", "api_key"),
                            client_id = LireCat("sumup_bank2", "client_id"),
                            client_secret = LireCat("sumup_bank2", "client_secret")
                        }
                    }
                };
                string oxapayApiKey = LireCat("oxapay", "api_key");
                RepondreJson(response, 200, new { iptv, telegramMode, sumupMode, sumup, oxapayApiKey });
            }
            else if (path == "/api/admin/settings/telegram" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);
                string mode = doc.RootElement.TryGetProperty("mode", out var modeElem) ? (modeElem.GetString() ?? "").Trim() : "";
                if (mode != "webhook" && mode != "polling")
                {
                    RepondreJson(response, 400, new { success = false, message = "Mode Telegram invalide. Doit être webhook ou polling." });
                    return;
                }

                await Program.AppliquerModeTelegram(botClient, mode);
                RepondreJson(response, 200, new { success = true, mode = config.ModeTelegram });
            }
            else if (path == "/api/admin/settings/sumup/mode" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);
                string mode = doc.RootElement.TryGetProperty("mode", out var modeElem) ? (modeElem.GetString() ?? "").Trim() : "";
                if (mode != "webhook" && mode != "polling")
                {
                    RepondreJson(response, 400, new { success = false, message = "Mode SumUp invalide. Doit être webhook ou polling." });
                    return;
                }

                Program.AppliquerModeSumUp(botClient, mode);
                RepondreJson(response, 200, new { success = true, mode = config.ModeSumUp });
            }
            else if (path == "/api/admin/settings/sumup" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);
                var root = doc.RootElement;

                string active = root.TryGetProperty("active", out var actElem) ? (actElem.GetString() ?? "").Trim() : "";
                if (active != "sumup" && active != "sumup_bank2")
                {
                    RepondreJson(response, 400, new { success = false, message = "Banque active invalide. Doit être sumup ou sumup_bank2." });
                    return;
                }

                string LireBanque(string cat)
                {
                    if (!root.TryGetProperty("banks", out var banks) || banks.ValueKind != JsonValueKind.Object)
                        return "Bloc banks manquant.";
                    if (!banks.TryGetProperty(cat, out var bank) || bank.ValueKind != JsonValueKind.Object)
                        return "Banque " + cat + " manquante.";
                    foreach (string k in config.SumUpBankKeys)
                    {
                        string val = bank.TryGetProperty(k, out var el) ? (el.GetString() ?? "").Trim() : "";
                        if (string.IsNullOrWhiteSpace(val))
                            return cat + "." + k + " vide. Tout doit être en table.";
                    }
                    foreach (string k in config.SumUpBankKeys)
                    {
                        string val = bank.TryGetProperty(k, out var el) ? (el.GetString() ?? "").Trim() : "";
                        config.SetSetting(cat, k, val);
                    }
                    return "";
                }

                string err1 = LireBanque("sumup");
                if (!string.IsNullOrEmpty(err1))
                {
                    RepondreJson(response, 400, new { success = false, message = err1 });
                    return;
                }
                string err2 = LireBanque("sumup_bank2");
                if (!string.IsNullOrEmpty(err2))
                {
                    RepondreJson(response, 400, new { success = false, message = err2 });
                    return;
                }

                string expRaw = "";
                if (root.TryGetProperty("expiration_minutes", out var expElem))
                    expRaw = GetJsonStringOrNumber(expElem).Trim();
                if (!int.TryParse(expRaw, out int expMin) || expMin <= 0)
                {
                    RepondreJson(response, 400, new { success = false, message = "Expiration SumUp invalide. Doit être un nombre de minutes > 0." });
                    return;
                }

                config.SumUpActiveBank = active;
                config.SetSetting("general", "sumup_expiration_minutes", expMin.ToString());
                RepondreJson(response, 200, new { success = true, bank = config.SumUpActiveCategory });
            }
            else if (path == "/api/admin/settings/oxapay" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);
                string apiKey = doc.RootElement.TryGetProperty("api_key", out var kElem) ? (kElem.GetString() ?? "").Trim() : "";
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    RepondreJson(response, 400, new { success = false, message = "Clé OxaPay vide. La clé doit être en DB." });
                    return;
                }
                config.SetSetting("oxapay", "api_key", apiKey);
                RepondreJson(response, 200, new { success = true });
            }
            else if (path == "/api/admin/settings/iptv" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);
                var root = doc.RootElement;

                if (root.TryGetProperty("host", out var hostElem)) config.SetSetting("iptv", "host", hostElem.GetString() ?? "");
                if (root.TryGetProperty("type", out var tElem)) config.SetSetting("iptv", "type", tElem.GetString() ?? "");
                if (root.TryGetProperty("message_footer", out var footElem)) config.SetSetting("iptv", "message_footer", footElem.GetString() ?? "");
                if (root.TryGetProperty("price_1m", out var p1)) config.SetSetting("iptv", "price_1m", p1.ValueKind == JsonValueKind.Number ? p1.GetRawText() : (p1.GetString() ?? ""));
                if (root.TryGetProperty("price_3m", out var p3)) config.SetSetting("iptv", "price_3m", p3.ValueKind == JsonValueKind.Number ? p3.GetRawText() : (p3.GetString() ?? ""));
                if (root.TryGetProperty("price_6m", out var p6)) config.SetSetting("iptv", "price_6m", p6.ValueKind == JsonValueKind.Number ? p6.GetRawText() : (p6.GetString() ?? ""));
                if (root.TryGetProperty("price_12m", out var p12)) config.SetSetting("iptv", "price_12m", p12.ValueKind == JsonValueKind.Number ? p12.GetRawText() : (p12.GetString() ?? ""));

                if (root.TryGetProperty("accounts", out var accElem) && accElem.ValueKind == JsonValueKind.Array)
                {
                    var accounts = new List<iptv.IptvAccount>();
                    foreach (var item in accElem.EnumerateArray())
                    {
                        accounts.Add(new iptv.IptvAccount
                        {
                            Name = item.TryGetProperty("name", out var nEl) ? nEl.GetString() ?? "" : "",
                            ApiKey = item.TryGetProperty("api_key", out var kEl) ? kEl.GetString() ?? "" : "",
                            ApiUrl = item.TryGetProperty("api_url", out var uEl) ? uEl.GetString() ?? "" : "",
                            Pack = item.TryGetProperty("pack", out var pkEl) ? pkEl.GetString() ?? "" : "",
                            Active = item.TryGetProperty("active", out var actEl) && actEl.ValueKind == JsonValueKind.True
                        });
                    }
                    accounts = iptv.PurgerIncomplets(accounts);
                    config.SetSetting("iptv", "accounts", JsonSerializer.Serialize(accounts));
                }

                if (root.TryGetProperty("panel_accounts", out var panelElem) && panelElem.ValueKind == JsonValueKind.Array)
                {
                    var panelAccounts = new List<IptvPanel.IptvPanelAccount>();
                    foreach (var item in panelElem.EnumerateArray())
                    {
                        panelAccounts.Add(new IptvPanel.IptvPanelAccount
                        {
                            Name = item.TryGetProperty("name", out var nEl) ? nEl.GetString() ?? "" : "",
                            Username = item.TryGetProperty("username", out var uEl) ? uEl.GetString() ?? "" : "",
                            Password = item.TryGetProperty("password", out var pEl) ? pEl.GetString() ?? "" : "",
                            Active = item.TryGetProperty("active", out var actEl) && actEl.ValueKind == JsonValueKind.True
                        });
                    }
                    config.SetSetting("iptv", "panel_accounts", JsonSerializer.Serialize(IptvPanel.NormalizeActive(panelAccounts)));
                    IptvPanel.ResetAuthCache();
                }

                RepondreJson(response, 200, new { success = true });
            }
            else if (path == "/api/admin/iptv/panel-test" && request.HttpMethod == "POST")
            {
                try
                {
                    IptvPanel.ResetAuthCache();
                    await IptvPanel.AuthenticateSession();
                    var stats = await IptvPanel.GetResellerPanelStats();
                    RepondreJson(response, 200, new { success = true, stats });
                }
                catch (Exception ex)
                {
                    RepondreJson(response, 400, new { success = false, message = ex.Message });
                }
            }
            else if (path == "/api/admin/settings/password" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);
                string newPassword = doc.RootElement.TryGetProperty("password", out var pElem) ? pElem.GetString() ?? "" : "";

                if (string.IsNullOrWhiteSpace(newPassword))
                {
                    RepondreJson(response, 400, new { success = false, message = "Mot de passe invalide" });
                    return;
                }

                EnregistrerMotDePasseAdmin(newPassword);
                _adminSessions.Clear();
                string session = CreerSessionAdmin();
                RepondreJson(response, 200, new { success = true, message = "Mot de passe mis à jour avec succès", token = session });
            }
            else
            {
                RepondreJson(response, 404, new { success = false, message = "Endpoint non trouvé" });
            }
        }

        private static void AjouterHeadersSecuriteAntiIndexation(HttpListenerResponse response)
        {
            try
            {
                response.Headers.Add("X-Robots-Tag", "noindex, nofollow, noarchive, nosnippet");
                response.Headers.Add("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0");
                response.Headers.Add("Pragma", "no-cache");
            }
            catch { }
        }

        private static async Task GererFichiersStatiques(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            string rawUrl = request.Url?.AbsolutePath ?? "/";

            AjouterHeadersSecuriteAntiIndexation(response);

            string slug = config.AdminSlug.Trim('/');
            string secretPrefix = "/" + slug;

            if (!rawUrl.Equals(secretPrefix, StringComparison.OrdinalIgnoreCase) && !rawUrl.StartsWith(secretPrefix + "/", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = 404;
                response.Close();
                return;
            }

            string relativePath = rawUrl.Substring(secretPrefix.Length);
            if (string.IsNullOrWhiteSpace(relativePath) || relativePath == "/")
            {
                relativePath = "/index.html";
            }

            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Administration", "Web");
            if (!Directory.Exists(baseDir))
            {
                baseDir = Path.Combine(Directory.GetCurrentDirectory(), "Administration", "Web");
            }

            string filePath = Path.Combine(baseDir, relativePath.TrimStart('/'));
            string fullPath = Path.GetFullPath(filePath);
            if (!fullPath.StartsWith(Path.GetFullPath(baseDir)))
            {
                RepondreJson(response, 403, new { error = "Accès interdit" });
                return;
            }

            if (!File.Exists(fullPath))
            {
                response.StatusCode = 404;
                response.Close();
                return;
            }

            string contentType = "text/html";
            if (fullPath.EndsWith(".css")) contentType = "text/css";
            else if (fullPath.EndsWith(".js")) contentType = "application/javascript";
            else if (fullPath.EndsWith(".png")) contentType = "image/png";
            else if (fullPath.EndsWith(".ico")) contentType = "image/x-icon";
            else if (fullPath.EndsWith(".svg")) contentType = "image/svg+xml";
            else if (fullPath.EndsWith(".jpg") || fullPath.EndsWith(".jpeg")) contentType = "image/jpeg";
            else if (fullPath.EndsWith(".json")) contentType = "application/json";
            else if (fullPath.EndsWith(".woff2")) contentType = "font/woff2";
            else if (fullPath.EndsWith(".woff")) contentType = "font/woff";

            byte[] bytes = await File.ReadAllBytesAsync(fullPath);
            response.ContentType = contentType;
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            response.Close();
        }

        private static bool AutoriserTentativeLogin(string ip)
        {
            var now = DateTime.UtcNow;
            var entry = _loginAttempts.AddOrUpdate(ip,
                _ => (1, now),
                (_, prev) => prev.window.AddMinutes(10) < now ? (1, now) : (prev.count + 1, prev.window));
            return entry.count <= 8;
        }

        private static string HasherMotDePasse(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
            return $"pbkdf2${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        private static void EnregistrerMotDePasseAdmin(string password)
        {
            config.SetSetting("admin", "password", HasherMotDePasse(password));
        }

        private static bool VerifierMotDePasseAdmin(string password)
        {
            string stored = _adminSecretToken;
            if (string.IsNullOrEmpty(stored) || string.IsNullOrEmpty(password)) return false;

            if (stored.StartsWith("pbkdf2$", StringComparison.Ordinal))
            {
                string[] parts = stored.Split('$');
                if (parts.Length != 3) return false;
                byte[] salt = Convert.FromBase64String(parts[1]);
                byte[] expected = Convert.FromBase64String(parts[2]);
                byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
                return CryptographicOperations.FixedTimeEquals(expected, actual);
            }

            if (config.SecretsEgaux(password, stored))
            {
                EnregistrerMotDePasseAdmin(password);
                return true;
            }
            return false;
        }

        private static string CreerSessionAdmin()
        {
            string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            _adminSessions[token] = DateTime.UtcNow.AddHours(24);
            return token;
        }

        private static bool SessionAdminValide(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            if (!_adminSessions.TryGetValue(token, out DateTime exp)) return false;
            if (exp < DateTime.UtcNow)
            {
                _adminSessions.TryRemove(token, out _);
                return false;
            }
            return true;
        }

        private static bool SecretsEgaux(string fourni, string attendu)
        {
            if (string.IsNullOrEmpty(fourni) || string.IsNullOrEmpty(attendu))
            {
                return false;
            }

            byte[] a = Encoding.UTF8.GetBytes(fourni);
            byte[] b = Encoding.UTF8.GetBytes(attendu);
            if (a.Length != b.Length)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(a, b);
        }

        private static void RepondreJson(HttpListenerResponse response, int statusCode, object data)
        {
            try
            {
                AjouterHeadersSecuriteAntiIndexation(response);
                response.StatusCode = statusCode;
                response.ContentType = "application/json; charset=utf-8";
                string json = JsonSerializer.Serialize(data);
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                response.ContentLength64 = bytes.Length;
                response.OutputStream.Write(bytes, 0, bytes.Length);
                response.Close();
            }
            catch { }
        }

        static string GetJsonStringOrNumber(JsonElement el) => el.ValueKind == JsonValueKind.String ? el.GetString() ?? "" : el.GetRawText();
    }
}
