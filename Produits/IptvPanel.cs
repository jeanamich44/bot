using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ChezRheyyBot
{
    internal class IptvPanel
    {
        public class IptvPanelAccount
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("username")]
            public string Username { get; set; } = "";

            [JsonPropertyName("password")]
            public string Password { get; set; } = "";

            [JsonPropertyName("active")]
            public bool Active { get; set; }
        }

        private const string StaticW = "083dfde075a4172d32dd8ea12465b5a03fe6dd2db225003a924d77f8f7e55405529c7e4aac9d870a3dfd4400150608afd506d52cb3d79f054a371f79259a693d0aeb2b0aab9a2361118257af86ce9b8682434ed049efc8982100c523081532dfb4ed88631f231be497c9a1af0dc4da708959c5fafd8a14b0bb63a7085619b32fbdd91fd6b79f66276f5e21825ec5d0713def7155fa7ef3c84c272a22d8f8ddf9247ed60518b1f3cd6ca3230bda3ada3da6767683f0dde8747b8ad5dd9af804a334af471ab1266efb417d2cbd0c56b659d2c2d235bb839f361eb57abaa3fb2a3a2eca07098fc899c174c10414b0cbe671215dc2aa4afe8f18001520c0ecd029efffc4911f220de26c1eb4e7a38e357ebe1f4d19676f900945ac16c039ed84290d07aa3f39d45152250cfdb8977d0ca855183fb7d3149b680316a1457c5eea74cbcd90afa13854de0e7ef9c9fbfb96e208c31681d87c8d485bde8734fd70cd7b835dcf50ed3db4d1554e3cf6aa9de35566c422b8639fdb81b22e48a95ee4d1f244445651033005d8c8e3496aae71e34f32036a3f047311cf32ad8c20b80b3b42d6d87a258780d753b9bd39c497f39eeed4369eeb684425c6eb4a800c5a549ffe5619b26b85248021813abd502fdca1c8f06f8b8e7808a01bca87efaa304e2a3605de66ccc3036772540404aacf18ade80e8fa0063aea3e403b0a99acd70efd8d7c0dfbc0b98fa0662de3bda234f78f6f93";

        private static readonly string[] BouqList = new[]
        {
            "441","225","335","336","331","329","332","333","366","389","326","327","328","330","334","337","344","343","340","342","341","345","383","353",
            "351","359","346","347","348","349","356","371","357","352","369","354","355","358","360","361","362","363","364","372","365","368","370","376",
            "338","339","350","373","374","375","377","224","1","228","229","230","231","232","234","233","235","236","237","238","239","240","241","380",
            "242","245","413","384","246","247","248","421","390","392","393","394","395","422","424","412","399","400","401","414","415","257","443","261",
            "259","260","262","405","423","263","437","227","300","6","302","264","265","266","267","268","269","270","398","271","273","272","274","275",
            "276","277","382","278","279","280","281","282","283","284","285","286","287","288","381","289","290","291","292","293","294","295","298","297",
            "296","299","301","303","304","305","307","306","430","308","309","310","311","312","313","314","319","315","316","317","318","320","321","322",
            "323","324","325","5","178","183","185","186","221","425","200","191","192","198","187","189","190","193","194","220","196","219","197","199",
            "201","202","203","204","205","206","207","208","209","210","211","212","213","214","215","107","106","116","117","118","119","120","121","122",
            "108","109","110","112","111","184","222","113","114","115","123","124","442","125","126","127","176","128","129","131","132","133","407","179",
            "417","135","136","385","143","137","138","388","139","140","141","142","145","146","147","149","150","387","386","148","409","151","130","180",
            "144","153","152","378","379","154","155","181","156","223","157","158","159","160","161","428","162","163","182","164","165","166","167","168",
            "436","134","169","170","171","172","174","402","175","177","82","83","86","88","91","89","90","93","92","408","94","195","95","96","97",
            "216","98","99","100","101","102","103","104","105","3","9","79","8","10","406","11","429","63","42","12","13","14","15","21","17","18",
            "16","431","432","19","20","426","22","23","24","25","26","27","28","30","35","29","34","31","32","33","37","36","39","40","43","419",
            "74","73","72","71","70","69","68","67","44","438","433","46","47","434","435","48","49","41","420","78","77","76","75","45","54","81",
            "427","439","440","55","56","80","57","58","59","411","410","60","62","38","52","403","53","65","418","50","51","87","218","64","66","61",
            "217","416"
        };

        private static readonly object CacheLock = new object();
        private static Dictionary<string, string>? AuthHeaders;
        private static DateTime AuthUntil = DateTime.MinValue;
        private static Dictionary<string, string>? StatsCache;
        private static DateTime StatsUntil = DateTime.MinValue;
        private static List<Dictionary<string, string>>? ConnectionsCache;
        private static DateTime ConnectionsUntil = DateTime.MinValue;

        private static readonly CookieContainer Cookies = new();
        private static readonly HttpClient Http;

        static IptvPanel()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                CookieContainer = Cookies,
                UseCookies = true,
                AllowAutoRedirect = true
            };
            Http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(45) };
        }

        public static void ResetAuthCache()
        {
            lock (CacheLock)
            {
                AuthHeaders = null;
                AuthUntil = DateTime.MinValue;
                StatsCache = null;
                StatsUntil = DateTime.MinValue;
                ConnectionsCache = null;
                ConnectionsUntil = DateTime.MinValue;
                foreach (Cookie c in Cookies.GetAllCookies())
                    c.Expired = true;
            }
        }

        public static List<IptvPanelAccount> GetPanelAccounts()
        {
            string raw = config.GetSetting("iptv", "panel_accounts", "");
            if (string.IsNullOrWhiteSpace(raw)) return new List<IptvPanelAccount>();
            try
            {
                return JsonSerializer.Deserialize<List<IptvPanelAccount>>(raw) ?? new List<IptvPanelAccount>();
            }
            catch
            {
                return new List<IptvPanelAccount>();
            }
        }

        public static IptvPanelAccount? GetActivePanelAccount()
        {
            return GetPanelAccounts().FirstOrDefault(a =>
                a.Active &&
                !string.IsNullOrWhiteSpace(a.Username) &&
                !string.IsNullOrWhiteSpace(a.Password));
        }

        public static List<IptvPanelAccount> NormalizeActive(List<IptvPanelAccount> list)
        {
            bool seen = false;
            foreach (var a in list)
            {
                if (a.Active && !seen) seen = true;
                else a.Active = false;
            }
            if (!seen && list.Count > 0) list[0].Active = true;
            return list;
        }

        private static Dictionary<string, string> BaseHeaders() => new(StringComparer.OrdinalIgnoreCase)
        {
            ["accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7",
            ["accept-language"] = "fr-FR,fr;q=0.9,en-US;q=0.8,en;q=0.7",
            ["cache-control"] = "max-age=0",
            ["origin"] = "https://cms-4k.com",
            ["priority"] = "u=0, i",
            ["referer"] = "https://cms-4k.com/login",
            ["sec-ch-ua"] = "\"Chromium\";v=\"146\", \"Not)A;Brand\";v=\"24\", \"Google Chrome\";v=\"146\"",
            ["sec-ch-ua-mobile"] = "?0",
            ["sec-ch-ua-platform"] = "\"Windows\"",
            ["sec-fetch-dest"] = "document",
            ["sec-fetch-mode"] = "navigate",
            ["sec-fetch-site"] = "same-origin",
            ["sec-fetch-user"] = "?1",
            ["upgrade-insecure-requests"] = "1",
            ["user-agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/146.0.0.0 Safari/537.36"
        };

        private static async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, Dictionary<string, string> headers, HttpContent? content = null)
        {
            using var req = new HttpRequestMessage(method, url);
            foreach (var kv in headers)
            {
                if (kv.Key.Equals("content-type", StringComparison.OrdinalIgnoreCase)) continue;
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }
            if (content != null) req.Content = content;
            return await Http.SendAsync(req);
        }

        private static string? ExtraireCookie(HttpResponseMessage resp, string name)
        {
            if (!resp.Headers.TryGetValues("Set-Cookie", out var values)) return null;
            foreach (var v in values)
            {
                string part = v.Split(';')[0];
                int eq = part.IndexOf('=');
                if (eq > 0 && part[..eq].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                    return part[(eq + 1)..].Trim();
            }
            return null;
        }

        private static JsonElement ExtraireJsonp(string text, string callback)
        {
            var match = Regex.Match(text ?? "", Regex.Escape(callback) + @"\((.*)\)", RegexOptions.Singleline);
            if (!match.Success) throw new Exception("Impossible d'extraire le JSON du callback " + callback);
            using var doc = JsonDocument.Parse(match.Groups[1].Value);
            return doc.RootElement.Clone();
        }

        public static async Task<Dictionary<string, string>> AuthenticateSession()
        {
            lock (CacheLock)
            {
                if (AuthHeaders != null && DateTime.UtcNow < AuthUntil)
                    return new Dictionary<string, string>(AuthHeaders, StringComparer.OrdinalIgnoreCase);
            }

            var acc = GetActivePanelAccount() ?? throw new Exception("Aucun compte panel actif (user / mot de passe).");
            var headers = BaseHeaders();

            var respLogin = await SendAsync(HttpMethod.Get, "https://cms-4k.com/login", headers);
            string loginHtml = await respLogin.Content.ReadAsStringAsync();
            if ((int)respLogin.StatusCode != 200)
                throw new Exception("Echec du chargement de la page de login: " + (int)respLogin.StatusCode);

            var captchaMatch = Regex.Match(loginHtml, @"var captchaId\s*=\s*[""']([^""']+)[""']");
            if (!captchaMatch.Success) throw new Exception("Impossible de récupérer le captchaId depuis la page");
            string captchaId = captchaMatch.Groups[1].Value;

            string? stormersessid = ExtraireCookie(respLogin, "STORMERSESSID");
            if (string.IsNullOrWhiteSpace(stormersessid))
                stormersessid = Cookies.GetCookies(new Uri("https://cms-4k.com/"))["STORMERSESSID"]?.Value;
            if (string.IsNullOrWhiteSpace(stormersessid)) throw new Exception("Cookie STORMERSESSID introuvable");

            var sessionHeaders = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase)
            {
                ["cookie"] = "STORMERSESSID=" + stormersessid
            };

            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string cbLoad = "geetest_" + (Random.Shared.Next(10000) + timestamp);
            var geetestHeaders = sessionHeaders
                .Where(kv => !kv.Key.Equals("cookie", StringComparison.OrdinalIgnoreCase) && !kv.Key.Equals("origin", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

            string loadUrl = "https://gcaptcha4.geetest.com/load?callback=" + Uri.EscapeDataString(cbLoad)
                + "&captcha_id=" + Uri.EscapeDataString(captchaId)
                + "&client_type=web&lang=eng";
            var respLoad = await SendAsync(HttpMethod.Get, loadUrl, geetestHeaders);
            string loadText = await respLoad.Content.ReadAsStringAsync();
            if ((int)respLoad.StatusCode != 200) throw new Exception("Echec du chargement de Geetest load");

            var loadJson = ExtraireJsonp(loadText, cbLoad);
            var loadData = loadJson.TryGetProperty("data", out var ld) ? ld : default;

            string cbVerify = "geetest_" + (Random.Shared.Next(10000) + timestamp);
            string verifyUrl = "https://gcaptcha4.geetest.com/verify?callback=" + Uri.EscapeDataString(cbVerify)
                + "&captcha_id=" + Uri.EscapeDataString(captchaId)
                + "&client_type=web"
                + "&lot_number=" + Uri.EscapeDataString(GetJsonString(loadData, "lot_number"))
                + "&payload=" + Uri.EscapeDataString(GetJsonString(loadData, "payload"))
                + "&process_token=" + Uri.EscapeDataString(GetJsonString(loadData, "process_token"))
                + "&payload_protocol=1&pt=1&w=" + Uri.EscapeDataString(StaticW);
            var respVerify = await SendAsync(HttpMethod.Get, verifyUrl, geetestHeaders);
            string verifyText = await respVerify.Content.ReadAsStringAsync();
            if ((int)respVerify.StatusCode != 200) throw new Exception("Echec de la vérification Geetest");

            var verifyJson = ExtraireJsonp(verifyText, cbVerify);
            var seccode = default(JsonElement);
            if (verifyJson.TryGetProperty("data", out var vd) && vd.TryGetProperty("seccode", out var sc))
                seccode = sc;

            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["uname"] = acc.Username,
                ["upass"] = acc.Password,
                ["lot_number"] = GetJsonString(seccode, "lot_number"),
                ["captcha_output"] = GetJsonString(seccode, "captcha_output"),
                ["pass_token"] = GetJsonString(seccode, "pass_token"),
                ["gen_time"] = GetJsonString(seccode, "gen_time"),
                ["btn-login"] = ""
            });

            var postHeaders = new Dictionary<string, string>(sessionHeaders, StringComparer.OrdinalIgnoreCase)
            {
                ["content-type"] = "application/x-www-form-urlencoded"
            };
            var respPost = await SendAsync(HttpMethod.Post, "https://cms-4k.com/login.php", postHeaders, form);
            string postHtml = await respPost.Content.ReadAsStringAsync();
            if ((int)respPost.StatusCode != 200 || !postHtml.Contains("Dashboard | 4K"))
                throw new Exception("Echec de la connexion au Dashboard");

            lock (CacheLock)
            {
                AuthHeaders = sessionHeaders;
                AuthUntil = DateTime.UtcNow.AddSeconds(1200);
            }
            return new Dictionary<string, string>(sessionHeaders, StringComparer.OrdinalIgnoreCase);
        }

        private static string GetJsonString(JsonElement el, string key)
        {
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString() ?? "";
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var n) && n.ValueKind == JsonValueKind.Number)
                return n.GetRawText();
            return "";
        }

        public static async Task<Dictionary<string, string>> GetResellerPanelStats()
        {
            lock (CacheLock)
            {
                if (StatsCache != null && DateTime.UtcNow < StatsUntil)
                    return new Dictionary<string, string>(StatsCache);
            }

            var auth = await AuthenticateSession();
            var resp = await SendAsync(HttpMethod.Get, "https://cms-4k.com/addnew?t=lines", auth);
            string html = await resp.Content.ReadAsStringAsync();
            if ((int)resp.StatusCode != 200) throw new Exception("Echec du chargement de la page de statistiques");

            var matchDemo = Regex.Match(html, @"Remaining Demo</p>\s*<h5[^>]*>([^<]+)</h5>", RegexOptions.IgnoreCase);
            var matchBalance = Regex.Match(html, @"Balance:\s*<b[^>]*>(\d+)</b>", RegexOptions.IgnoreCase);
            var data = new Dictionary<string, string>
            {
                ["credits"] = matchBalance.Success ? matchBalance.Groups[1].Value : "Inconnu",
                ["remaining_demos"] = matchDemo.Success ? matchDemo.Groups[1].Value.Trim() : "Inconnu"
            };
            lock (CacheLock)
            {
                StatsCache = data;
                StatsUntil = DateTime.UtcNow.AddSeconds(120);
            }
            return data;
        }

        public static async Task<Dictionary<string, string>> GenerateDemoIptvLine()
        {
            string username = (char)('a' + Random.Shared.Next(26)) + string.Concat(Enumerable.Range(0, 6).Select(_ =>
            {
                const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
                return chars[Random.Shared.Next(chars.Length)];
            }));

            var demo = new Dictionary<string, object?>
            {
                ["mac"] = username,
                ["sub_id"] = "8",
                ["comment"] = "",
                ["bouq_list"] = BouqList,
                ["type"] = "lines",
                ["bouq_custom"] = "",
                ["country"] = "[\"FR\"]"
            };
            string jsonData = JsonSerializer.Serialize(demo);
            string apiUrl = "https://cms-4k.com/api.php?action=add_new&data=" + Uri.EscapeDataString(jsonData);

            var auth = await AuthenticateSession();
            var respApi = await SendAsync(HttpMethod.Get, apiUrl, auth);
            string apiText = await respApi.Content.ReadAsStringAsync();
            bool isJson = EstJson(respApi, apiText);
            if (!isJson)
            {
                ResetAuthCache();
                auth = await AuthenticateSession();
                respApi = await SendAsync(HttpMethod.Get, apiUrl, auth);
                apiText = await respApi.Content.ReadAsStringAsync();
                if ((int)respApi.StatusCode != 200)
                    throw new Exception("Echec de l'appel API de création après ré-authentification");
            }

            using var apiDoc = JsonDocument.Parse(apiText);
            if (!(apiDoc.RootElement.TryGetProperty("result", out var resultEl) && resultEl.ValueKind == JsonValueKind.True))
                throw new Exception("Echec de la création de la démo: " + apiText);

            var (tableJson, headersTable) = await ChargerTableLignes(auth);
            var lines = tableJson.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array
                ? dataEl.EnumerateArray().ToList()
                : new List<JsonElement>();

            JsonElement? created = null;
            foreach (var line in lines)
            {
                if (line.TryGetProperty("username", out var u) && (u.GetString() ?? "") == username)
                {
                    created = line.Clone();
                    break;
                }
            }
            if (created == null) throw new Exception("La ligne créée est introuvable dans la table");
            if (!created.Value.TryGetProperty("encrypted_id", out var encEl) || string.IsNullOrWhiteSpace(encEl.GetString()))
                throw new Exception("encrypted_id manquant pour la ligne");
            string encryptedId = encEl.GetString()!;

            string vpnUrl = "https://cms-4k.com/options_list.php?reqvalue=copy_m3u&sub=&id=" + Uri.EscapeDataString(encryptedId)
                + "&action=vpn&_=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var respVpn = await SendAsync(HttpMethod.Get, vpnUrl, headersTable);
            string vpnHtml = await respVpn.Content.ReadAsStringAsync();
            if ((int)respVpn.StatusCode != 200) throw new Exception("Echec de la récupération des détails de ligne");

            var userMatch = Regex.Match(vpnHtml, @"var username\s*=\s*'([^']*)'");
            var passMatch = Regex.Match(vpnHtml, @"var password\s*=\s*'([^']*)'");
            if (!userMatch.Success || !passMatch.Success) throw new Exception("Impossible d'extraire les identifiants");

            string extractedUser = userMatch.Groups[1].Value;
            string extractedPass = passMatch.Groups[1].Value;
            string serverBase = string.IsNullOrWhiteSpace(iptv.Host) ? "http://cf.business-cloud-neo.com" : iptv.Host.Trim().TrimEnd('/');
            return new Dictionary<string, string>
            {
                ["username"] = extractedUser,
                ["password"] = extractedPass,
                ["domain"] = serverBase,
                ["server"] = serverBase,
                ["xtream_server"] = serverBase,
                ["m3u_url"] = $"{serverBase}/get.php?username={extractedUser}&password={extractedPass}&type=m3u_plus&output=ts"
            };
        }

        public static async Task<List<Dictionary<string, string>>> GetActiveWatchingLines()
        {
            lock (CacheLock)
            {
                if (ConnectionsCache != null && DateTime.UtcNow < ConnectionsUntil)
                    return ConnectionsCache.Select(d => new Dictionary<string, string>(d)).ToList();
            }

            var auth = await AuthenticateSession();
            var (tableJson, _) = await ChargerTableLignes(auth);
            var online = new List<Dictionary<string, string>>();
            if (tableJson.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var line in dataEl.EnumerateArray())
                {
                    string active = line.TryGetProperty("active", out var a) ? a.GetString() ?? "" : "";
                    if (active != "online") continue;
                    online.Add(new Dictionary<string, string>
                    {
                        ["username"] = line.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "",
                        ["connections"] = JsonVersTexte(line, "connections"),
                        ["watching"] = JsonVersTexte(line, "watching"),
                        ["country_code"] = line.TryGetProperty("country_code", out var c) ? c.GetString() ?? "" : ""
                    });
                }
            }
            lock (CacheLock)
            {
                ConnectionsCache = online;
                ConnectionsUntil = DateTime.UtcNow.AddSeconds(20);
            }
            return online;
        }

        private static string JsonVersTexte(JsonElement line, string key)
        {
            if (!line.TryGetProperty(key, out var p)) return "";
            return p.ValueKind == JsonValueKind.String ? (p.GetString() ?? "") : p.GetRawText();
        }

        private static bool EstJson(HttpResponseMessage resp, string text)
        {
            string ct = resp.Content.Headers.ContentType?.MediaType ?? "";
            string t = (text ?? "").Trim();
            return ct.Contains("application/json", StringComparison.OrdinalIgnoreCase) || t.StartsWith("{") || t.StartsWith("[");
        }

        private static async Task<(JsonDocument doc, Dictionary<string, string> headers)> ChargerTableLignes(Dictionary<string, string> authenticatedHeaders)
        {
            var qs = new List<string>
            {
                "draw=1",
                "order[0][column]=0",
                "order[0][dir]=desc",
                "start=0",
                "length=200",
                "search[value]=",
                "search[regex]=false",
                "id=lines",
                "filter=15",
                "state=0",
                "reseller=",
                "template=0",
                "_=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            string[] columns = { "id", "username", "password", "link_flag", "exp_date_flag", "package_flag", "reseller_notes", "owner", "status", "active", "speed_percent", "connections", "watching", "created_at", "country_code", "" };
            for (int i = 0; i < columns.Length; i++)
            {
                qs.Add($"columns[{i}][data]={Uri.EscapeDataString(columns[i])}");
                qs.Add($"columns[{i}][searchable]=true");
                qs.Add($"columns[{i}][orderable]={(i == 4 ? "true" : "false")}");
                qs.Add($"columns[{i}][search][value]=");
                qs.Add($"columns[{i}][search][regex]=false");
            }

            var headersTable = new Dictionary<string, string>(authenticatedHeaders, StringComparer.OrdinalIgnoreCase)
            {
                ["accept"] = "application/json, text/javascript, */*; q=0.01",
                ["x-requested-with"] = "XMLHttpRequest",
                ["referer"] = "https://cms-4k.com/users?t=lines"
            };

            string url = "https://cms-4k.com/api_table.php?" + string.Join("&", qs);
            var resp = await SendAsync(HttpMethod.Get, url, headersTable);
            string text = await resp.Content.ReadAsStringAsync();
            if (!EstJson(resp, text))
            {
                ResetAuthCache();
                var auth = await AuthenticateSession();
                headersTable = new Dictionary<string, string>(auth, StringComparer.OrdinalIgnoreCase)
                {
                    ["accept"] = "application/json, text/javascript, */*; q=0.01",
                    ["x-requested-with"] = "XMLHttpRequest",
                    ["referer"] = "https://cms-4k.com/users?t=lines"
                };
                resp = await SendAsync(HttpMethod.Get, url, headersTable);
                text = await resp.Content.ReadAsStringAsync();
                if ((int)resp.StatusCode != 200)
                    throw new Exception("Echec de la récupération de la table de lignes après ré-authentification");
            }
            return (JsonDocument.Parse(text), headersTable);
        }
    }
}
