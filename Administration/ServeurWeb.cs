using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using Telegram.Bot;

namespace ChezRheyyBot
{
    internal class ServeurWeb
    {
        private static string _adminSecretToken => config.GetSetting("admin", "password", "");

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
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);
                string pwd = doc.RootElement.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "";

                if (pwd == _adminSecretToken)
                {
                    config.IncAdminLogins();
                    RepondreJson(response, 200, new { success = true, token = pwd });
                }
                else
                {
                    RepondreJson(response, 401, new { success = false, message = "Mot de passe incorrect" });
                }
                return;
            }

            string authHeader = request.Headers["Authorization"] ?? "";
            string tokenProvided = authHeader.StartsWith("Bearer ") ? authHeader.Substring(7).Trim() : "";

            if (tokenProvided != _adminSecretToken)
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
                int totalUsers = config.UserSave.Count;
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

                var nowParis = DataBase.ConvertirEnHeureParis(DateTime.UtcNow);
                var todayStart = nowParis.Date;

                var todayBlocks = new List<object>();
                for (int i = 0; i < 24; i += 2)
                {
                    var blockStart = todayStart.AddHours(i);
                    var blockEnd = todayStart.AddHours(i + 2);
                    int txCount = transactions.Count(t => {
                        var d = DataBase.ConvertirEnHeureParis(t.CreatedAt);
                        return d >= blockStart && d < blockEnd;
                    });
                    int payCount = allPayments.Count(p => {
                        var d = DataBase.ConvertirEnHeureParis(p.CreatedAt);
                        return d >= blockStart && d < blockEnd;
                    });
                    todayBlocks.Add(new { label = $"{i:D2}h-{(i+2):D2}h", volume = txCount + payCount });
                }

                var last7Days = new List<object>();
                for (int i = 6; i >= 0; i--)
                {
                    var dayDate = todayStart.AddDays(-i);
                    var dayEnd = dayDate.AddDays(1);
                    int txCount = transactions.Count(t => {
                        var d = DataBase.ConvertirEnHeureParis(t.CreatedAt);
                        return d >= dayDate && d < dayEnd;
                    });
                    int payCount = allPayments.Count(p => {
                        var d = DataBase.ConvertirEnHeureParis(p.CreatedAt);
                        return d >= dayDate && d < dayEnd;
                    });
                    last7Days.Add(new { label = dayDate.ToString("dd/MM"), volume = txCount + payCount });
                }

                var last30Days = new List<object>();
                for (int i = 29; i >= 0; i--)
                {
                    var dayDate = todayStart.AddDays(-i);
                    var dayEnd = dayDate.AddDays(1);
                    int txCount = transactions.Count(t => {
                        var d = DataBase.ConvertirEnHeureParis(t.CreatedAt);
                        return d >= dayDate && d < dayEnd;
                    });
                    int payCount = allPayments.Count(p => {
                        var d = DataBase.ConvertirEnHeureParis(p.CreatedAt);
                        return d >= dayDate && d < dayEnd;
                    });
                    last30Days.Add(new { label = dayDate.ToString("dd/MM"), volume = txCount + payCount });
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
                        var dayEnd = dayDate.AddDays(1);
                        int txCount = transactions.Count(t => {
                            var d = DataBase.ConvertirEnHeureParis(t.CreatedAt);
                            return d >= dayDate && d < dayEnd;
                        });
                        int payCount = allPayments.Count(p => {
                            var d = DataBase.ConvertirEnHeureParis(p.CreatedAt);
                            return d >= dayDate && d < dayEnd;
                        });
                        customBlocks.Add(new { label = dayDate.ToString("dd/MM"), volume = txCount + payCount });
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
                DataBase.ChargerUtilisateurs();
                var users = config.UserSave.Select(u => new
                {
                    userNumber = config.ObtenirOuCreerNumeroUtilisateur(u.Item1),
                    id = u.Item1,
                    username = config.ObtenirUsername(u.Item1),
                    achats = u.Item2,
                    solde = u.Item3,
                    isBanned = u.Item4,
                    banReason = config.BanReasons.TryGetValue(u.Item1, out var r) ? r : ""
                }).OrderBy(u => u.userNumber).ToList();

                RepondreJson(response, 200, new { users });
            }
            else if (path == "/api/admin/users/solde" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);
                var root = doc.RootElement;

                long userId = long.Parse(root.GetProperty("userId").GetString() ?? "0");
                string action = root.GetProperty("action").GetString() ?? "add";
                double amount = root.GetProperty("amount").GetDouble();

                int idx = config.UserSave.FindIndex(u => u.Item1 == userId);
                if (idx != -1)
                {
                    var old = config.UserSave[idx];
                    double newSolde = action == "add" ? old.Item3 + amount : old.Item3 - amount;
                    if (newSolde < 0) newSolde = 0.0;
                    config.UserSave[idx] = Tuple.Create(old.Item1, old.Item2, newSolde, old.Item4);
                    DataBase.SauvegarderUtilisateurIndividuel(userId);
                    RepondreJson(response, 200, new { success = true });
                }
                else
                {
                    RepondreJson(response, 404, new { success = false, message = "Utilisateur introuvable" });
                }
            }
            else if (path == "/api/admin/users/ban" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);
                var root = doc.RootElement;

                long userId = long.Parse(root.GetProperty("userId").GetString() ?? "0");
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

                int idx = config.UserSave.FindIndex(u => u.Item1 == userId);
                if (idx != -1)
                {
                    var old = config.UserSave[idx];
                    config.UserSave[idx] = Tuple.Create(old.Item1, old.Item2, old.Item3, ban);
                }
                else if (ban)
                {
                    config.UserSave.Add(Tuple.Create(userId, 0, 0.0, true));
                }

                DataBase.SauvegarderUtilisateurIndividuel(userId);
                RepondreJson(response, 200, new { success = true });
            }
            else if (path == "/api/admin/users/delete" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);

                long userId = doc.RootElement.GetProperty("userId").GetInt64();
                bool deleted = DataBase.SupprimerUtilisateurCompletBDD(userId);
                RepondreJson(response, 200, new { success = deleted });
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
                        int v = elem.TryGetProperty("value", out var vE) ? (vE.ValueKind == JsonValueKind.Number ? vE.GetInt32() : (int.TryParse(vE.GetString(), out int parsedV) ? parsedV : 0)) : 0;
                        double pr = elem.TryGetProperty("price", out var prE) ? (prE.ValueKind == JsonValueKind.Number ? prE.GetDouble() : (double.TryParse(prE.GetString(), out double parsedP) ? parsedP : 0.0)) : 0.0;

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
                    int value = root.GetProperty("value").GetInt32();
                    double price = root.GetProperty("price").GetDouble();

                    DataBase.InsererDansStock(brand, code, pin, value, price);
                    RepondreJson(response, 200, new { success = true, count = 1 });
                }
            }
            else if (path == "/api/admin/stock/delete" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);
                int id = doc.RootElement.GetProperty("id").GetInt32();

                bool deleted = DataBase.SupprimerStockParId(id);
                RepondreJson(response, 200, new { success = deleted });
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
                var iptv = config.CategorySettings.TryGetValue("iptv", out var dict) ? dict : new Dictionary<string, string>();
                string telegramMode = config.ModeTelegram;
                string sumupMode = config.ModeSumUp;
                RepondreJson(response, 200, new { iptv, telegramMode, sumupMode });
            }
            else if (path == "/api/admin/settings/telegram" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);
                string mode = doc.RootElement.GetProperty("mode").GetString() ?? "polling";

                await Program.AppliquerModeTelegram(botClient, mode);
                RepondreJson(response, 200, new { success = true, mode = config.ModeTelegram });
            }
            else if (path == "/api/admin/settings/iptv" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string bodyStr = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyStr);
                var root = doc.RootElement;

                if (root.TryGetProperty("price_1m", out var p1)) config.SetSetting("iptv", "price_1m", p1.ValueKind == JsonValueKind.Number ? p1.GetRawText() : (p1.GetString() ?? "5"));
                if (root.TryGetProperty("price_3m", out var p3)) config.SetSetting("iptv", "price_3m", p3.ValueKind == JsonValueKind.Number ? p3.GetRawText() : (p3.GetString() ?? "10"));
                if (root.TryGetProperty("price_6m", out var p6)) config.SetSetting("iptv", "price_6m", p6.ValueKind == JsonValueKind.Number ? p6.GetRawText() : (p6.GetString() ?? "15"));
                if (root.TryGetProperty("price_12m", out var p12)) config.SetSetting("iptv", "price_12m", p12.ValueKind == JsonValueKind.Number ? p12.GetRawText() : (p12.GetString() ?? "30"));

                RepondreJson(response, 200, new { success = true });
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

                config.SetSetting("admin", "password", newPassword);
                RepondreJson(response, 200, new { success = true, message = "Mot de passe mis à jour avec succès" });
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
    }
}
