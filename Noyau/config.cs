using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ChezRheyyBot
{
    internal class config
    {
        public static string apiKey => GetSetting("oxapay", "api_key", Environment.GetEnvironmentVariable("OXAPAY_API_KEY") ?? "");

        public static Dictionary<string, string> PayementLink = new Dictionary<string, string>();
        public static List<string> IdPaiement = new List<string>();
        public static ConcurrentDictionary<string, byte> CustomPaiement = new ConcurrentDictionary<string, byte>();

        public static string botToken => Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? throw new InvalidOperationException("Variable d'environnement TELEGRAM_BOT_TOKEN manquante.");

        public static readonly object UsersLock = new object();
        public static List<Utilisateur> UserSave = new List<Utilisateur>();
        public static string debugMode = "run";

        public static List<string> idAdmins = new List<string>();

        public static List<string> BanniUser = new List<string>();
        public static Dictionary<long, string> BanReasons = new Dictionary<long, string>();
        public static Dictionary<long, int> UserNumbers = new Dictionary<long, int>();
        public static Dictionary<long, string> Usernames = new Dictionary<long, string>();

        private static readonly AsyncLocal<BotContexte> _contexte = new AsyncLocal<BotContexte>();

        public static void ResetContexte() => _contexte.Value = new BotContexte();

        private static BotContexte Ctx
        {
            get
            {
                if (_contexte.Value == null) _contexte.Value = new BotContexte();
                return _contexte.Value;
            }
        }

        public static string CurrentChatId
        {
            get => Ctx.ChatId;
            set => Ctx.ChatId = value ?? "";
        }

        public static string CurrentPseudo
        {
            get => Ctx.Pseudo;
            set => Ctx.Pseudo = value ?? "";
        }

        public static string msgId
        {
            get => Ctx.MsgId;
            set => Ctx.MsgId = value ?? "";
        }

        public static string ObtenirUsername(long userId)
        {
            lock (UsersLock)
            {
                if (Usernames.TryGetValue(userId, out string? username) && !string.IsNullOrWhiteSpace(username))
                {
                    return username.Trim();
                }
            }
            return "";
        }

        public static int ObtenirOuCreerNumeroUtilisateur(long userId)
        {
            lock (UsersLock)
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
        }

        public static Utilisateur ObtenirOuCreerUtilisateur(long id)
        {
            lock (UsersLock)
            {
                var u = UserSave.FirstOrDefault(x => x.Id == id);
                if (u != null) return u;
                u = new Utilisateur { Id = id };
                UserSave.Add(u);
                return u;
            }
        }

        public static Utilisateur? TrouverUtilisateur(long id)
        {
            lock (UsersLock)
            {
                return UserSave.FirstOrDefault(x => x.Id == id);
            }
        }

        public static List<Utilisateur> CopierUtilisateurs()
        {
            lock (UsersLock)
            {
                return UserSave.Select(u => new Utilisateur
                {
                    Id = u.Id,
                    Achat = u.Achat,
                    Solde = u.Solde,
                    IsBanned = u.IsBanned
                }).ToList();
            }
        }

        public static void SynchroniserCacheUtilisateur(long id, int achat, double solde, bool isBanned)
        {
            lock (UsersLock)
            {
                var u = UserSave.FirstOrDefault(x => x.Id == id);
                if (u == null)
                {
                    UserSave.Add(new Utilisateur { Id = id, Achat = achat, Solde = solde, IsBanned = isBanned });
                }
                else
                {
                    u.Achat = achat;
                    u.Solde = solde;
                    u.IsBanned = isBanned;
                }
            }
        }

        public static Dictionary<string, string> IdMessage = new Dictionary<string, string>();
        public static double currentSolde = 0.0;
        public static int achat = 0;

        public static Dictionary<string, string> PayementAPI = new Dictionary<string, string>();
        public static ConcurrentDictionary<string, byte> AttentePaiement = new ConcurrentDictionary<string, byte>();
        public static Dictionary<string, string> MontantPayement = new Dictionary<string, string>();
        public static ConcurrentDictionary<string, long> CooldownPaiementUnix = new ConcurrentDictionary<string, long>();

        public static bool EstEnCooldownPaiement(string chatId)
        {
            PurgerCooldownsExpires();
            return CooldownPaiementUnix.ContainsKey(chatId);
        }

        public static void ActiverCooldownPaiement(string chatId, TimeSpan duree)
        {
            long until = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)duree.TotalSeconds;
            CooldownPaiementUnix[chatId] = until;
            PersisterCooldowns();
        }

        public static void RetirerCooldownPaiement(string chatId)
        {
            CooldownPaiementUnix.TryRemove(chatId, out _);
            PersisterCooldowns();
        }

        public static void PurgerCooldownsExpires()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var kv in CooldownPaiementUnix.ToArray())
            {
                if (kv.Value <= now) CooldownPaiementUnix.TryRemove(kv.Key, out _);
            }
        }

        public static void ChargerCooldowns()
        {
            string raw = GetSetting("general", "payment_cooldowns", "");
            if (string.IsNullOrWhiteSpace(raw)) return;
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, long>>(raw);
                if (dict == null) return;
                foreach (var kv in dict) CooldownPaiementUnix[kv.Key] = kv.Value;
                PurgerCooldownsExpires();
            }
            catch { }
        }

        private static void PersisterCooldowns()
        {
            PurgerCooldownsExpires();
            SetSetting("general", "payment_cooldowns", JsonSerializer.Serialize(new Dictionary<string, long>(CooldownPaiementUnix)));
        }

        public static string? DomainePublic()
        {
            string d = Environment.GetEnvironmentVariable("RAILWAY_PUBLIC_DOMAIN")
                ?? Environment.GetEnvironmentVariable("RAILWAY_STATIC_URL")
                ?? GetSetting("general", "public_domain", "")
                ?? "serveur-production-db21.up.railway.app";
            d = (d ?? "").Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(d)) d = "serveur-production-db21.up.railway.app";
            if (d.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) d = d[8..];
            if (d.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) d = d[7..];
            return string.IsNullOrWhiteSpace(d) ? "serveur-production-db21.up.railway.app" : d;
        }

        public static bool SecretsEgaux(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a.Length != b.Length) return false;
            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
        }

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

        public static string SumUpActiveBank
        {
            get => GetSetting("general", "sumup_active_bank", "sumup");
            set
            {
                SetSetting("general", "sumup_active_bank", value);
                paiement.ReinitialiserAccessToken();
            }
        }

        public static string SumUpActiveCategory => SumUpActiveBank.Equals("sumup_bank2", StringComparison.OrdinalIgnoreCase) || SumUpActiveBank.Equals("bank2", StringComparison.OrdinalIgnoreCase) || SumUpActiveBank == "2" ? "sumup_bank2" : "sumup";

        public static string AdminSlug
        {
            get => GetSetting("admin", "slug", "espace-sec-x9k2m7");
            set => SetSetting("admin", "slug", value);
        }

        public static string TelegramWebhookSecret
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("TELEGRAM_WEBHOOK_SECRET") ?? "";
                if (!string.IsNullOrWhiteSpace(env)) return env.Trim();

                string stored = GetSetting("telegram", "webhook_secret", "");
                if (!string.IsNullOrWhiteSpace(stored)) return stored.Trim();

                string generated = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
                SetSetting("telegram", "webhook_secret", generated);
                return generated;
            }
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

        public static readonly object SettingsLock = new object();
        public static bool IsMetricsLoadedFromDb = false;

        public static void ChargerMetricsFromSettings()
        {
            lock (SettingsLock)
            {
                if (!CategorySettings.TryGetValue("metrics", out var dict))
                {
                    IsMetricsLoadedFromDb = true;
                    return;
                }

                long ParseVal(string key, long current)
                {
                    return dict.TryGetValue(key, out var s) && long.TryParse(s, out long val) ? Math.Max(val, current) : current;
                }

                MetricTelegramReceived = ParseVal("telegram_received", MetricTelegramReceived);
                MetricTelegramSent = ParseVal("telegram_sent", MetricTelegramSent);
                MetricSumUpReceived = ParseVal("sumup_received", MetricSumUpReceived);
                MetricSumUpSent = ParseVal("sumup_sent", MetricSumUpSent);
                MetricOxaPayReceived = ParseVal("oxapay_received", MetricOxaPayReceived);
                MetricOxaPaySent = ParseVal("oxapay_sent", MetricOxaPaySent);
                MetricCommandsExecuted = ParseVal("commands_executed", MetricCommandsExecuted);
                MetricErrorsCount = ParseVal("errors_count", MetricErrorsCount);
                MetricAdminLogins = ParseVal("admin_logins", MetricAdminLogins);

                IsMetricsLoadedFromDb = true;
            }
        }

        public static void PersisterMetricsInSettings()
        {
            lock (SettingsLock)
            {
                if (!IsMetricsLoadedFromDb) return;

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
        }

        public static List<string> categorie = new List<string>();
        public static Dictionary<string, Dictionary<string, string>> CategorySettings = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, string> Settings = new Dictionary<string, string>();
        public static Dictionary<string, string> ProfileSettings => Settings;

        public static string GetSetting(string category, string key, string defaultValue = "")
        {
            lock (SettingsLock)
            {
                if (CategorySettings.TryGetValue(category, out var dict) && dict.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
                {
                    return val;
                }
                return defaultValue;
            }
        }

        public static void SetSetting(string category, string key, string value)
        {
            lock (SettingsLock)
            {
                if (!CategorySettings.ContainsKey(category))
                {
                    CategorySettings[category] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                CategorySettings[category][key] = value;
            }
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
            ChargerCooldowns();
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
