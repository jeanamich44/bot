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

        public static string apiKey => config.GetSetting("iptv", "api_key", "c348fb1b8882dcf4cc4854b7f8d88f61");
        public static string apiUrl => config.GetSetting("iptv", "api_url", "https://4k.cms-only.ru/api/api.php");
        public static string apiPack => config.GetSetting("iptv", "pack", "43551");
        public static string apiType => config.GetSetting("iptv", "type", "m3u");

        public static async Task<string> GenerateIPTV(string date, string userId = "")
        {
            string key = apiKey;
            if (string.IsNullOrWhiteSpace(key)) key = "c348fb1b8882dcf4cc4854b7f8d88f61";

            string baseApi = apiUrl.Trim();
            if (string.IsNullOrWhiteSpace(baseApi)) baseApi = "https://4k.cms-only.ru/api/api.php";

            string noteText = !string.IsNullOrWhiteSpace(userId) ? $"Achat Bot Telegram: {userId}" : $"order_{Guid.NewGuid():N}"[..14];
            string encodedNote = Uri.EscapeDataString(noteText);
            string sep = baseApi.Contains("?") ? "&" : "?";
            string url = $"{baseApi}{sep}action=new&type={apiType}&sub={date}&pack={apiPack}&country=FR&notes={encodedNote}&api_key={key}";
            Console.WriteLine($"[IPTV INFO] Début génération IPTV pour {date} mois.");
            Console.WriteLine($"[IPTV CONFIG] API URL: {baseApi} | Key: {key} | Pack: {apiPack} | Type: {apiType}");
            Console.WriteLine($"[IPTV REQUEST] {url}");

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
                    string[] fallbackUrls = new[]
                    {
                        "https://4k.cms-only.ru/api/api.php",
                        "https://cms-4k.com/api/api.php",
                        "http://cf.business-cloud-neo.com/api.php"
                    };

                    foreach (var fb in fallbackUrls)
                    {
                        if (fb.Equals(baseApi, StringComparison.OrdinalIgnoreCase)) continue;
                        string fbSep = fb.Contains("?") ? "&" : "?";
                        string fbUrl = $"{fb}{fbSep}action=new&type={apiType}&sub={date}&pack={apiPack}&country=&notes={encodedNote}&api_key={key}";
                        Console.WriteLine($"[IPTV RETRY FALLBACK] Tentative sur URL de secours: {fbUrl}");
                        var fbResponse = await client.GetAsync(fbUrl);
                        Console.WriteLine($"[IPTV HTTP STATUS FALLBACK] {(int)fbResponse.StatusCode} {fbResponse.ReasonPhrase}");
                        if (fbResponse.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            response = fbResponse;
                            break;
                        }
                    }
                }

                string content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[IPTV RESPONSE RAW] {content}");

                string trimmed = content.Trim();
                if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[IPTV PARSED DIRECT] {trimmed}");
                    return trimmed;
                }

                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    var root = doc.RootElement;
                    string extracted = ExtractUrlFromJson(root);
                    if (!string.IsNullOrWhiteSpace(extracted))
                    {
                        Console.WriteLine($"[IPTV PARSED JSON] {extracted}");
                        return extracted;
                    }
                }
                catch (Exception jsonEx)
                {
                    Console.WriteLine($"[IPTV JSON PARSE EXCEPTION] {jsonEx.Message}");
                }

                var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"https?://[^\s""'<>\\]+");
                if (match.Success)
                {
                    Console.WriteLine($"[IPTV PARSED REGEX] {match.Value}");
                    return match.Value;
                }

                Console.WriteLine("[IPTV ERROR] Aucune URL n'a pu être extraite de la réponse du serveur.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IPTV EXCEPTION] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }

            return string.Empty;
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
