using Newtonsoft.Json;

namespace ChezRheyyBot
{
    internal class config
    {
        public readonly static string apiUrl = "https://api.oxapay.com/v1/payment/invoice";
        public readonly static string apiKey = Environment.GetEnvironmentVariable("OXAPAY_API_KEY") ?? throw new InvalidOperationException("Variable d'environnement OXAPAY_API_KEY manquante.");

        public static Dictionary<string, string> PayementLink = new Dictionary<string, string>();
        public static List<string> IdPaiement = new List<string>();
        public static List<string> CustomPaiement = new List<string>();

        public static readonly string botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? throw new InvalidOperationException("Variable d'environnement TELEGRAM_BOT_TOKEN manquante.");

        public static List<Tuple<long, int, double, bool>> UserSave = new List<Tuple<long, int, double, bool>>();
        public static string debugMode = "run";

        public static List<string> idAdmins = new List<string>();
        public static string idAdmin = "6298536933";

        public static List<string> BanniUser = new List<string>();
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
        public static Dictionary<string, string> ProfileSettings = new Dictionary<string, string>();

        public static void InitialiseAdmin()
        {
            idAdmins.Add("8676919760");
            idAdmins.Add("6298536933");
            idAdmins.Add("8740419947");
        }

        public static void InitialiseCategorie()
        {
            categorie.Add("Carrefour");
            categorie.Add("Quick");
        }

        public static void GetProfileSettings()
        {
            DataBase.ChargerProfile();
        }

        public static void SetProfileSettings()
        {
            DataBase.SauvegarderProfile();
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
