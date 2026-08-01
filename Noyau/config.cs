namespace ChezRheyyBot
{
    internal class config
    {
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
        public static Dictionary<long, int> UserNumbers = new Dictionary<long, int>();
        public static Dictionary<long, string> Usernames = new Dictionary<long, string>();

        public static string ObtenirUsername(long userId)
        {
            if (Usernames.TryGetValue(userId, out string? username) && !string.IsNullOrWhiteSpace(username))
            {
                string clean = username.Trim();
                return clean.StartsWith("@") ? clean : "@" + clean;
            }
            return "";
        }

        public static int ObtenirOuCreerNumeroUtilisateur(long userId)
        {
            if (UserNumbers.TryGetValue(userId, out int num))
            {
                return num;
            }

            if (userId == 6298536933) num = 1;
            else if (userId == 8740419947) num = 2;
            else if (userId == 8676919760) num = 3;
            else if (userId == 5883885733) num = 4;
            else
            {
                int max = UserNumbers.Values.DefaultIfEmpty(0).Max();
                if (max < 4) max = 4;
                num = max + 1;
            }

            UserNumbers[userId] = num;
            return num;
        }
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

        public static bool ModeMaintenance
        {
            get => GetSetting("general", "maintenance", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
            set => SetSetting("general", "maintenance", value ? "true" : "false");
        }

        public static string ModeTelegram
        {
            get => GetSetting("general", "telegram_mode", "webhook");
            set => SetSetting("general", "telegram_mode", value);
        }

        public static string ModeSumUp
        {
            get => GetSetting("general", "sumup_mode", "webhook");
            set => SetSetting("general", "sumup_mode", value);
        }

        public static string AdminSlug
        {
            get => GetSetting("admin", "slug", "espace-sec-x9k2m7");
            set => SetSetting("admin", "slug", value);
        }

        public static bool blockstart = false;
        public static bool promotion = false;

        // Métriques et Statistiques Système (RAM + stockage Settings)
        public static long MetricTelegramReceived = 0;
        public static long MetricTelegramSent = 0;
        public static long MetricSumUpReceived = 0;
        public static long MetricSumUpSent = 0;
        public static long MetricOxaPayReceived = 0;
        public static long MetricOxaPaySent = 0;
        public static long MetricCommandsExecuted = 0;
        public static long MetricErrorsCount = 0;
        public static long MetricAdminLogins = 0;

        public static void IncTelegramReceived() => System.Threading.Interlocked.Increment(ref MetricTelegramReceived);
        public static void IncTelegramSent() => System.Threading.Interlocked.Increment(ref MetricTelegramSent);
        public static void IncSumUpReceived() => System.Threading.Interlocked.Increment(ref MetricSumUpReceived);
        public static void IncSumUpSent() => System.Threading.Interlocked.Increment(ref MetricSumUpSent);
        public static void IncOxaPayReceived() => System.Threading.Interlocked.Increment(ref MetricOxaPayReceived);
        public static void IncOxaPaySent() => System.Threading.Interlocked.Increment(ref MetricOxaPaySent);
        public static void IncCommandsExecuted() => System.Threading.Interlocked.Increment(ref MetricCommandsExecuted);
        public static void IncErrorsCount() => System.Threading.Interlocked.Increment(ref MetricErrorsCount);
        public static void IncAdminLogins() => System.Threading.Interlocked.Increment(ref MetricAdminLogins);

        public static void ChargerMetricsFromSettings()
        {
            MetricTelegramReceived = long.TryParse(GetSetting("metrics", "telegram_received", "0"), out long tr) ? tr : 0;
            MetricTelegramSent = long.TryParse(GetSetting("metrics", "telegram_sent", "0"), out long ts) ? ts : 0;
            MetricSumUpReceived = long.TryParse(GetSetting("metrics", "sumup_received", "0"), out long sr) ? sr : 0;
            MetricSumUpSent = long.TryParse(GetSetting("metrics", "sumup_sent", "0"), out long ss) ? ss : 0;
            MetricOxaPayReceived = long.TryParse(GetSetting("metrics", "oxapay_received", "0"), out long or) ? or : 0;
            MetricOxaPaySent = long.TryParse(GetSetting("metrics", "oxapay_sent", "0"), out long os) ? os : 0;
            MetricCommandsExecuted = long.TryParse(GetSetting("metrics", "commands_executed", "0"), out long ce) ? ce : 0;
            MetricErrorsCount = long.TryParse(GetSetting("metrics", "errors_count", "0"), out long ec) ? ec : 0;
            MetricAdminLogins = long.TryParse(GetSetting("metrics", "admin_logins", "0"), out long al) ? al : 0;
        }

        public static void PersisterMetricsInSettings()
        {
            if (!CategorySettings.ContainsKey("metrics"))
            {
                CategorySettings["metrics"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            var dict = CategorySettings["metrics"];
            dict["telegram_received"] = MetricTelegramReceived.ToString();
            dict["telegram_sent"] = MetricTelegramSent.ToString();
            dict["sumup_received"] = MetricSumUpReceived.ToString();
            dict["sumup_sent"] = MetricSumUpSent.ToString();
            dict["oxapay_received"] = MetricOxaPayReceived.ToString();
            dict["oxapay_sent"] = MetricOxaPaySent.ToString();
            dict["commands_executed"] = MetricCommandsExecuted.ToString();
            dict["errors_count"] = MetricErrorsCount.ToString();
            dict["admin_logins"] = MetricAdminLogins.ToString();
        }

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
