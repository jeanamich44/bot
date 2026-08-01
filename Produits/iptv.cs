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

        public static string apiKey => config.GetSetting("iptv", "api_key", "16b9b89931169d6a4fd534c10e24ebad");
        public static string apiUrl => config.GetSetting("iptv", "api_url", "https://4k.cms-only.ru/api/api.php");
        public static string apiPack => config.GetSetting("iptv", "pack", "43551");
        public static string apiType => config.GetSetting("iptv", "type", "m3u");

        public static async Task<string> GenerateIPTV(string date)
        {
            string url = $"{apiUrl}" +
                         $"?action=new" +
                         $"&type={apiType}" +
                         $"&sub={date}" +
                         $"&pack={apiPack}" +
                         $"&country=" +
                         $"&notes=" +
                         $"&api_key={apiKey}";

            try
            {
                using HttpClient client = new HttpClient();
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string jsonResult = await response.Content.ReadAsStringAsync();

                ApiResponse data = JsonSerializer.Deserialize<ApiResponse>(jsonResult);

                if (data != null && data.Status == "true")
                {
                    Console.WriteLine("URL IPTV : " + data.Url);
                    return data.Url;
                }
                else
                {
                    Console.WriteLine("Erreur API : " + data?.Message);
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine("Erreur HTTP : " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur : " + ex.Message);
            }

            return string.Empty;
        }
    }
}
