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

        public static List<IptvAccount> GetAccounts()
        {
            string raw = config.GetSetting("iptv", "accounts", "");
            if (string.IsNullOrWhiteSpace(raw)) return new List<IptvAccount>();
            try
            {
                return JsonSerializer.Deserialize<List<IptvAccount>>(raw) ?? new List<IptvAccount>();
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
