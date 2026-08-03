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
            string url = $"{apiUrl}?action=new&type={apiType}&sub={date}&pack={apiPack}&country=&notes=&api_key={apiKey}";
            Console.WriteLine($"[IPTV Request] {url}");

            try
            {
                using HttpClient client = new HttpClient();
                HttpResponseMessage response = await client.GetAsync(url);
                string jsonResult = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[IPTV Response] {jsonResult}");

                using var doc = JsonDocument.Parse(jsonResult);
                var root = doc.RootElement;

                string status = "";
                if (root.TryGetProperty("status", out var stElem))
                {
                    if (stElem.ValueKind == JsonValueKind.True) status = "true";
                    else if (stElem.ValueKind == JsonValueKind.String) status = stElem.GetString() ?? "";
                }

                string resUrl = "";
                if (root.TryGetProperty("url", out var urlElem) && urlElem.ValueKind == JsonValueKind.String)
                {
                    resUrl = urlElem.GetString() ?? "";
                }

                if ((status.ToLower() == "true" || status == "1") && !string.IsNullOrWhiteSpace(resUrl))
                {
                    Console.WriteLine("URL IPTV : " + resUrl);
                    return resUrl;
                }
                else if (!string.IsNullOrWhiteSpace(resUrl))
                {
                    return resUrl;
                }
                else
                {
                    string msg = root.TryGetProperty("message", out var msgElem) ? msgElem.GetString() ?? "" : "";
                    Console.WriteLine("Erreur API IPTV: " + msg);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur IPTV: " + ex.Message);
            }

            return string.Empty;
        }
    }
}
