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

        public static string apiKey => config.GetSetting("iptv", "api_key", "");
        public static string apiUrl => config.GetSetting("iptv", "api_url", "http://cf.business-cloud-neo.com/api/api.php");
        public static string apiPack => config.GetSetting("iptv", "pack", "43551");
        public static string apiType => config.GetSetting("iptv", "type", "m3u");

        public static async Task<string> GenerateIPTV(string date)
        {
            string key = apiKey;
            string baseApi = apiUrl.Trim();
            string sep = baseApi.Contains("?") ? "&" : "?";

            string url = $"{baseApi}{sep}action=new&type={apiType}&sub={date}&pack={apiPack}&country=&notes=&api_key={key}&key={key}";
            Console.WriteLine($"[IPTV Request] {url}");

            try
            {
                using HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                HttpResponseMessage response = await client.GetAsync(url);
                string content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[IPTV Response] {content}");

                string trimmed = content.Trim();
                if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed;
                }

                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    var root = doc.RootElement;
                    string extracted = ExtractUrlFromJson(root);
                    if (!string.IsNullOrWhiteSpace(extracted)) return extracted;
                }
                catch { }

                var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"https?://[^\s""'<>\\]+");
                if (match.Success)
                {
                    return match.Value;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur IPTV: " + ex.Message);
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
