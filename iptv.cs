using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UgcBotTG
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

        public static string apiKey = "7d825d543f4582de824e83046d0aa8fa";
        public static async Task<string> GenerateIPTV(string date)
        {
            string url = $"https://cms-4k.com/api/api.php" +
                         $"?action=new" +
                         $"&type=m3u" +
                         $"&sub={date}" +
                         $"&pack=43551" +
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
