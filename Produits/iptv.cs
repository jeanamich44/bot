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

            string url = $"{baseApi}{sep}action=new&type={apiType}&sub={date}&pack={apiPack}&country=&notes=&api_key={key}";
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

                    if (root.TryGetProperty("url", out var urlElem) && urlElem.ValueKind == JsonValueKind.String)
                    {
                        string resUrl = urlElem.GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(resUrl)) return resUrl;
                    }

                    if (root.TryGetProperty("link", out var linkElem) && linkElem.ValueKind == JsonValueKind.String)
                    {
                        string resLink = linkElem.GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(resLink)) return resLink;
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur IPTV: " + ex.Message);
            }

            return string.Empty;
        }
    }
}
