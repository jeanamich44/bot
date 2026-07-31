namespace ChezRheyyBot
{
    internal class config
    {
        public readonly static string apiUrl = "https://api.oxapay.com/v1/payment/invoice";
        public static string apiKey => GetSetting("oxapay", "api_key", Environment.GetEnvironmentVariable("OXAPAY_API_KEY") ?? "");

        public static Dictionary<string, string> PayementLink = new Dictionary<string, string>();
        public static List<string> IdPaiement = new List<string>();
        public static List<string> CustomPaiement = new List<string>();

        public static string botToken => Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? throw new InvalidOperationException("Variable d'environnement TELEGRAM_BOT_TOKEN manquante.");

        public static List<Tuple<long, int, double, bool>> UserSave = new List<Tuple<long, int, double, bool>>();
        public static string debugMode = "run";

        public static List<string> idAdmins = new List<string>();

        public static List<string> BanniUser = new List<string>();
        public static Dictionary<long, string> BanReasons = new Dictionary<long, string>();
        public static string CurrentChatId = "";
        public static Dictionary<string, string> IdMessage = new Dictionary<string, string>();
        public static string msgId = "";
        public static string CurrentPseudo = "";
        public static double currentSolde = 0.0;
        public static int achat = 0;

        public static Dictionary<string, string> PayementAPI = new Dictionary<string, string>();
        public static List<string> AttentePaiement = new List<string>();
        public static Dictionary<string, string> MontantPayement = new Dictionary<string, string>();
        public static List<string> banAPI = new List<string>();

        public static bool blockstart = false;
        public static bool promotion = false;

        public static List<string> categorie = new List<string>();
        public static Dictionary<string, Dictionary<string, string>> CategorySettings = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, string> Settings = new Dictionary<string, string>();
        public static Dictionary<string, string> ProfileSettings => Settings;

        public static string GetSetting(string category, string key, string defaultValue = "")
        {
            if (CategorySettings.TryGetValue(category, out var dict) && dict.TryGetValue(key, out var val))
            {
                return val;
            }
            return defaultValue;
        }

        public static void SetSetting(string category, string key, string value)
        {
            if (!CategorySettings.ContainsKey(category))
            {
                CategorySettings[category] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            CategorySettings[category][key] = value;
            DataBase.SauvegarderSettings();
        }

        public static void InitialiseAdmin()
        {
        }

        public static void InitialiseCategorie()
        {
            categorie.Clear();
            categorie.Add("Carrefour");
        }

        public static void GetProfileSettings()
        {
            DataBase.ChargerSettings();
        }

        public static void SetProfileSettings()
        {
            DataBase.SauvegarderSettings();
        }

        public static async Task ReadJson()
        {
            DataBase.ChargerUtilisateurs();
        }

        public static void JsonWrite()
        {
            DataBase.SauvegarderUtilisateurs();
        }
    }
}
