using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ChezRheyyBot
{
    internal class iptv
    {
        public class ApiResponse
        {
            [JsonPropertyName("status")]
            public string Status { get; set; }

            [JsonPropertyName("user_id")]
            public string UserId { get; set; }

            [JsonPropertyName("notes")]
            public string Notes { get; set; }

            [JsonPropertyName("country")]
            public string Country { get; set; }

            [JsonPropertyName("message")]
            public string Message { get; set; }

            [JsonPropertyName("url")]
            public string Url { get; set; }
        }

        public class IptvAccount
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("api_key")]
            public string ApiKey { get; set; } = "";

            [JsonPropertyName("api_url")]
            public string ApiUrl { get; set; } = "";

            [JsonPropertyName("pack")]
            public string Pack { get; set; } = "";

            [JsonPropertyName("active")]
            public bool Active { get; set; }
        }

        public static string apiType => config.GetSetting("iptv", "type", "");
        public static string Host => config.GetSetting("iptv", "host", "");
        public static string MessageFooter => config.GetSetting("iptv", "message_footer", "");
        public static double PrixDemo
        {
            get
            {
                if (!double.TryParse(config.GetSetting("iptv", "price_demo", "").Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double p) || p <= 0)
                    throw new InvalidOperationException("iptv.price_demo manquant ou invalide en DB.");
                return p;
            }
        }
        public static bool DemoEnabled
        {
            get
            {
                string v = (config.GetSetting("iptv", "demo_enabled", "") ?? "").Trim();
                if (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("on", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (v == "0" || v.Equals("false", StringComparison.OrdinalIgnoreCase) || v.Equals("off", StringComparison.OrdinalIgnoreCase))
                    return false;
                throw new InvalidOperationException("iptv.demo_enabled manquant ou invalide en DB.");
            }
            set => config.SetSetting("iptv", "demo_enabled", value ? "true" : "false");
        }

        public static void ValiderConfigOuCrash()
        {
            var manquantes = new List<string>();
            Dictionary<string, string>? d;
            lock (config.SettingsLock)
            {
                if (!config.CategorySettings.TryGetValue("iptv", out d) || d == null)
                    throw new InvalidOperationException("Config IPTV absente de la DB (catégorie iptv). Crash au démarrage.");
            }

            void Exiger(string key)
            {
                if (!d.TryGetValue(key, out var val) || string.IsNullOrWhiteSpace(val))
                    manquantes.Add("iptv." + key);
            }

            Exiger("host");
            Exiger("type");
            Exiger("message_footer");
            Exiger("price_1m");
            Exiger("price_3m");
            Exiger("price_6m");
            Exiger("price_12m");
            Exiger("price_demo");
            Exiger("demo_enabled");
            Exiger("accounts");
            Exiger("panel_accounts");

            foreach (var prixKey in new[] { "price_1m", "price_3m", "price_6m", "price_12m", "price_demo" })
            {
                if (d.TryGetValue(prixKey, out var raw) && !string.IsNullOrWhiteSpace(raw))
                {
                    if (!double.TryParse(raw.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double p) || p <= 0)
                        manquantes.Add("iptv." + prixKey + " (nombre invalide)");
                }
            }

            if (d.TryGetValue("demo_enabled", out var demoRaw) && !string.IsNullOrWhiteSpace(demoRaw))
            {
                string v = demoRaw.Trim();
                bool ok = v == "1" || v == "0"
                    || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("false", StringComparison.OrdinalIgnoreCase)
                    || v.Equals("on", StringComparison.OrdinalIgnoreCase) || v.Equals("off", StringComparison.OrdinalIgnoreCase);
                if (!ok) manquantes.Add("iptv.demo_enabled (valeur invalide)");
            }

            if (d.TryGetValue("accounts", out var accJson) && !string.IsNullOrWhiteSpace(accJson))
            {
                try
                {
                    var list = JsonSerializer.Deserialize<List<IptvAccount>>(accJson) ?? new List<IptvAccount>();
                    int complets = list.Count(a => !string.IsNullOrWhiteSpace(a.ApiKey) && !string.IsNullOrWhiteSpace(a.ApiUrl) && !string.IsNullOrWhiteSpace(a.Pack));
                    int actifs = list.Count(a => a.Active);
                    if (complets == 0) manquantes.Add("iptv.accounts (aucun compte API complet: api_key + api_url + pack)");
                    if (actifs != 1) manquantes.Add("iptv.accounts (il faut exactement un compte API actif)");
                }
                catch
                {
                    manquantes.Add("iptv.accounts (JSON invalide)");
                }
            }

            if (d.TryGetValue("panel_accounts", out var panelJson) && !string.IsNullOrWhiteSpace(panelJson))
            {
                try
                {
                    var list = JsonSerializer.Deserialize<List<IptvPanel.IptvPanelAccount>>(panelJson) ?? new List<IptvPanel.IptvPanelAccount>();
                    int complets = list.Count(a => !string.IsNullOrWhiteSpace(a.Username) && !string.IsNullOrWhiteSpace(a.Password));
                    int actifs = list.Count(a => a.Active);
                    if (complets == 0) manquantes.Add("iptv.panel_accounts (aucun compte panel complet: user + mot de passe)");
                    if (actifs != 1) manquantes.Add("iptv.panel_accounts (il faut exactement un compte panel actif)");
                }
                catch
                {
                    manquantes.Add("iptv.panel_accounts (JSON invalide)");
                }
            }

            if (manquantes.Count > 0)
            {
                throw new InvalidOperationException(
                    "Config IPTV incomplète en DB — crash au démarrage pour correction:\n- " + string.Join("\n- ", manquantes));
            }
        }

        public static bool EstComplet(IptvAccount a) =>
            a != null
            && !string.IsNullOrWhiteSpace(a.ApiKey)
            && !string.IsNullOrWhiteSpace(a.ApiUrl)
            && !string.IsNullOrWhiteSpace(a.Pack);

        public static List<IptvAccount> PurgerIncomplets(IEnumerable<IptvAccount>? list)
        {
            var kept = (list ?? Enumerable.Empty<IptvAccount>()).Where(EstComplet).ToList();
            bool seen = false;
            foreach (var a in kept)
            {
                if (a.Active && !seen) seen = true;
                else a.Active = false;
            }
            if (!seen && kept.Count > 0) kept[0].Active = true;
            return kept;
        }

        public static List<IptvAccount> GetAccounts()
        {
            string raw = config.GetSetting("iptv", "accounts", "");
            if (string.IsNullOrWhiteSpace(raw)) return new List<IptvAccount>();
            try
            {
                return PurgerIncomplets(JsonSerializer.Deserialize<List<IptvAccount>>(raw));
            }
            catch
            {
                return new List<IptvAccount>();
            }
        }

        public static IptvAccount? GetActiveAccount()
        {
            var accounts = GetAccounts();
            return accounts.FirstOrDefault(a =>
                a.Active &&
                !string.IsNullOrWhiteSpace(a.ApiKey) &&
                !string.IsNullOrWhiteSpace(a.ApiUrl) &&
                !string.IsNullOrWhiteSpace(a.Pack));
        }

        public static bool ActiverCompte(string choix, out string label, out string erreur)
        {
            label = "";
            erreur = "";
            var list = GetAccounts();
            if (list.Count == 0)
            {
                erreur = "Aucun compte API configuré.";
                return false;
            }

            int idx = -1;
            if (int.TryParse(choix, out int n) && n >= 1 && n <= list.Count)
                idx = n - 1;
            else
                idx = list.FindIndex(a => a.Name.Equals(choix, StringComparison.OrdinalIgnoreCase));

            if (idx < 0)
            {
                erreur = "Compte API introuvable. Utilise /compteapi 1, /compteapi 2, ou le nom du compte.";
                return false;
            }

            for (int i = 0; i < list.Count; i++)
                list[i].Active = i == idx;
            config.SetSetting("iptv", "accounts", JsonSerializer.Serialize(list));
            var acc = list[idx];
            label = string.IsNullOrWhiteSpace(acc.Name) ? $"Compte {idx + 1}" : acc.Name;
            return true;
        }

        public static async Task<string> GenerateIPTV(string date, string userId = "")
        {
            var acc = GetActiveAccount();
            if (acc == null)
            {
                Console.WriteLine("[IPTV ERROR] Aucun compte API actif (coche un compte dans le panel).");
                return string.Empty;
            }

            string type = apiType;
            if (string.IsNullOrWhiteSpace(type))
            {
                Console.WriteLine("[IPTV ERROR] Type de flux IPTV manquant en base.");
                return string.Empty;
            }

            string noteText = !string.IsNullOrWhiteSpace(userId) ? $"Achat Bot Telegram: {userId}" : $"order_{Guid.NewGuid():N}"[..14];
            string encodedNote = Uri.EscapeDataString(noteText);
            string baseApi = acc.ApiUrl.Trim();
            string sep = baseApi.Contains("?") ? "&" : "?";
            string url = $"{baseApi}{sep}action=new&type={type}&sub={date}&pack={acc.Pack.Trim()}&country=FR&notes={encodedNote}&api_key={acc.ApiKey.Trim()}";
            string label = string.IsNullOrWhiteSpace(acc.Name) ? acc.Pack : acc.Name;
            Console.WriteLine($"[IPTV INFO] Génération {date} mois via le compte actif: {label} | Pack: {acc.Pack} | Type: {type}");

            try
            {
                using HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "fr-FR,fr;q=0.9,en-US;q=0.8,en;q=0.7");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");

                HttpResponseMessage response = await client.GetAsync(url);
                Console.WriteLine($"[IPTV HTTP STATUS] {(int)response.StatusCode} {response.ReasonPhrase}");
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    Console.WriteLine("[IPTV ERROR] Le compte actif a échoué. Aucun autre compte n'est tenté.");
                    return string.Empty;
                }

                string content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[IPTV RESPONSE RAW] {content}");
                string extracted = ExtraireUrlReponse(content);
                if (!string.IsNullOrWhiteSpace(extracted)) return extracted;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IPTV EXCEPTION] {ex.GetType().Name}: {ex.Message}");
            }

            Console.WriteLine("[IPTV ERROR] Le compte actif n'a pas renvoyé d'identifiants. Aucun débit.");
            return string.Empty;
        }

        private static string ExtraireUrlReponse(string content)
        {
            string trimmed = (content ?? "").Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return trimmed;

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                string extracted = ExtractUrlFromJson(doc.RootElement);
                if (!string.IsNullOrWhiteSpace(extracted)) return extracted;
            }
            catch (Exception jsonEx)
            {
                Console.WriteLine($"[IPTV JSON PARSE EXCEPTION] {jsonEx.Message}");
            }

            var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"https?://[^\s""'<>\\]+");
            return match.Success ? match.Value : "";
        }

        private static string ExtractUrlFromJson(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                string[] urlKeys = new[] { "url", "link", "m3u_url", "playlist_url", "playlist", "download_link" };
                foreach (var k in urlKeys)
                {
                    if (element.TryGetProperty(k, out var p) && p.ValueKind == JsonValueKind.String)
                    {
                        string val = p.GetString() ?? "";
                        if (val.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || val.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                            return val;
                    }
                }

                foreach (var prop in element.EnumerateObject())
                {
                    string childRes = ExtractUrlFromJson(prop.Value);
                    if (!string.IsNullOrWhiteSpace(childRes)) return childRes;
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    string childRes = ExtractUrlFromJson(item);
                    if (!string.IsNullOrWhiteSpace(childRes)) return childRes;
                }
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                string str = element.GetString() ?? "";
                if (str.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || str.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    return str;
            }

            return string.Empty;
        }
    }
}
