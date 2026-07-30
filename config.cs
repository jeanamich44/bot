using Newtonsoft.Json;

namespace UgcBotTG
{
    internal class config
    {
        public readonly static string apiUrl = "https://api.oxapay.com/v1/payment/invoice";
        public readonly static string apiKey = Environment.GetEnvironmentVariable("OXAPAY_API_KEY") ?? "XZSCYQ-MDQCKO-1VNVKO-BBCWR7";

        public static Dictionary<string, string> PayementLink = new Dictionary<string, string>();
        public static List<string> IdPaiement = new List<string>();
        public static List<string> CustomPaiement = new List<string>();

        public static readonly string botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? "8210003748:AAGS5av_jxsfhoxy4esmYhU1Nu3RuaXVV3k";

        public static List<Tuple<long, int, double>> UserSave = new List<Tuple<long, int, double>>();
        public static string debugMode = "run";

        public static List<string> idAdmins = new List<string>();
        public static string idAdmin = "6298536933";

        public readonly static string UserFile = "settings/user.txt";
        public static readonly string dbPath = "stock.db";
        public readonly static string BanUser = "settings/ban.txt";



        public static List<string> BanniUser = new List<string>();
        public static string CurrentChatId = "";
        public static Dictionary<string,string> IdMessage = new Dictionary<string,string>();
        public static string msgId = "";
        public static string CurrentPseudo = "";
        public static double currentSolde = 0.0;
        public static int achat = 0;

        public static Dictionary<string,string> PayementAPI = new Dictionary<string,string>();
        public static List<string> AttentePaiement = new List<string>();
        public static Dictionary<string, string> MontantPayement = new Dictionary<string, string>();
        public static List<string> banAPI = new List<string>();

        public static bool blockstart = false;
        public static bool promotion = false;

        public static List<string> categorie = new List<string>();
        public static Dictionary<string,string> ProfileSettings = new Dictionary<string,string>();

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



        public static async Task HistoriqueAchat()
        {

        }



        public static void JsonWrite()
        {
            DataBase.SauvegarderUtilisateurs();
        }

        public static void ChargerBannie()
        {
            DataBase.ChargerBannis();
        }

        public static void EnregistrerBannie()
        {
            DataBase.SauvegarderBannis();
        }

        private class UserData
        {
            public UserData(long id, int achat, double solde)
            {
                this.id = id;
                this.achat = achat;
                this.solde = solde;
            }

            public long id { get; set; }
            public int achat { get; set; }
            public double solde { get; set; }
        }
    }
}
