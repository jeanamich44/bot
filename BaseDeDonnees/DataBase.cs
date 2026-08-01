using Npgsql;
using System.Text.Json;

namespace ChezRheyyBot
{
    internal class DataBase
    {
        public class StockItem
        {
            public int Id { get; set; }
            public string Value { get; set; }
            public string Code { get; set; }
            public string Price { get; set; }
            public string Pin { get; set; }
            public string Brand { get; set; }
        }

        public static string GetConnectionString()
        {
            string dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
                ?? Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL")
                ?? Environment.GetEnvironmentVariable("POSTGRES_URL")
                ?? "";

            if (!string.IsNullOrEmpty(dbUrl))
            {
                try
                {
                    if (dbUrl.StartsWith("postgres://") || dbUrl.StartsWith("postgresql://"))
                    {
                        var uri = new Uri(dbUrl);
                        string userInfo = uri.UserInfo;
                        string[] userParts = userInfo.Split(':');
                        string username = userParts[0];
                        string password = userParts.Length > 1 ? Uri.UnescapeDataString(userParts[1]) : "";
                        string host = uri.Host;
                        int port = uri.Port > 0 ? uri.Port : 5432;
                        string database = uri.AbsolutePath.TrimStart('/');

                        return $"Host={host};Port={port};Username={username};Password={password};Database={database};SslMode=Prefer;";
                    }
                    return dbUrl;
                }
                catch
                {

                }
            }

            string h = Environment.GetEnvironmentVariable("PGHOST") ?? "localhost";
            string u = Environment.GetEnvironmentVariable("PGUSER") ?? "postgres";
            string p = Environment.GetEnvironmentVariable("PGPASSWORD") ?? "postgres";
            string d = Environment.GetEnvironmentVariable("PGDATABASE") ?? "postgres";
            string pt = Environment.GetEnvironmentVariable("PGPORT") ?? "5432";

            return $"Host={h};Port={pt};Username={u};Password={p};Database={d};SslMode=Prefer;";
        }

        public static void CreerTableStockSiExistePas()
        {
            using (var connexion = new NpgsqlConnection(GetConnectionString()))
            {
                connexion.Open();

                string requete = @"
                CREATE TABLE IF NOT EXISTS stock (
                    Id SERIAL PRIMARY KEY,
                    Brand TEXT,
                    Code TEXT,
                    Pin TEXT,
                    Value INTEGER,
                    Price DOUBLE PRECISION
                );
                CREATE TABLE IF NOT EXISTS users (
                    Id BIGINT PRIMARY KEY,
                    Achat INTEGER DEFAULT 0,
                    Solde DOUBLE PRECISION DEFAULT 0.0,
                    IsBanned BOOLEAN DEFAULT FALSE
                );
                ALTER TABLE users ADD COLUMN IF NOT EXISTS IsBanned BOOLEAN DEFAULT FALSE;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS IsAdmin BOOLEAN DEFAULT FALSE;
                CREATE TABLE IF NOT EXISTS settings (
                    Key TEXT PRIMARY KEY,
                    Value JSONB
                );

                CREATE TABLE IF NOT EXISTS transactions (
                    Id SERIAL PRIMARY KEY,
                    UserId BIGINT DEFAULT 0,
                    Brand TEXT NOT NULL,
                    Code TEXT,
                    Pin TEXT,
                    Value INTEGER DEFAULT 0,
                    Price DOUBLE PRECISION DEFAULT 0.0,
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );
                CREATE TABLE IF NOT EXISTS payments (
                    Id SERIAL PRIMARY KEY,
                    ChatId TEXT NOT NULL,
                    TrackId TEXT UNIQUE NOT NULL,
                    Amount DOUBLE PRECISION NOT NULL,
                    PaymentMethod TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    PaymentUrl TEXT,
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );";

                using (var commande = new NpgsqlCommand(requete, connexion))
                {
                    commande.ExecuteNonQuery();
                }
            }
        }

        public static void InsererDansStock(string brand, string code, string pin, int value, double price)
        {
            using (var connexion = new NpgsqlConnection(GetConnectionString()))
            {
                connexion.Open();

                string requete = @"
                INSERT INTO stock (Brand, Code, Pin, Value, Price)
                VALUES (@brand, @code, @pin, @value, @price);";

                using (var commande = new NpgsqlCommand(requete, connexion))
                {
                    commande.Parameters.AddWithValue("@brand", brand);
                    commande.Parameters.AddWithValue("@code", code);
                    commande.Parameters.AddWithValue("@pin", pin);
                    commande.Parameters.AddWithValue("@value", value);
                    commande.Parameters.AddWithValue("@price", price);
                    commande.ExecuteNonQuery();
                }
            }
        }

        public static int InsererStockEnMasse(List<StockItem> items)
        {
            if (items == null || items.Count == 0) return 0;
            int count = 0;
            using (var connexion = new NpgsqlConnection(GetConnectionString()))
            {
                connexion.Open();
                using (var transaction = connexion.BeginTransaction())
                {
                    string requete = @"
                    INSERT INTO stock (Brand, Code, Pin, Value, Price)
                    VALUES (@brand, @code, @pin, @value, @price);";

                    foreach (var item in items)
                    {
                        using (var commande = new NpgsqlCommand(requete, connexion, transaction))
                        {
                            commande.Parameters.AddWithValue("@brand", string.IsNullOrEmpty(item.Brand) ? "carr" : item.Brand);
                            commande.Parameters.AddWithValue("@code", item.Code ?? "");
                            commande.Parameters.AddWithValue("@pin", item.Pin ?? "");
                            commande.Parameters.AddWithValue("@value", int.TryParse(item.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out int v) ? v : 0);
                            commande.Parameters.AddWithValue("@price", double.TryParse(item.Price, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double p) ? p : 0.0);
                            commande.ExecuteNonQuery();
                            count++;
                        }
                    }
                    transaction.Commit();
                }
            }
            return count;
        }

        public static List<StockItem> ObtenirStocksParBrand(string brand)
        {
            var resultats = new List<StockItem>();

            using (var connexion = new NpgsqlConnection(GetConnectionString()))
            {
                connexion.Open();
                string requete = "SELECT Id, Value, Price, Code, Pin FROM stock WHERE Brand = @brand";

                using (var cmd = new NpgsqlCommand(requete, connexion))
                {
                    cmd.Parameters.AddWithValue("@brand", brand);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            resultats.Add(new StockItem
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                Value = reader["Value"].ToString(),
                                Price = reader["Price"].ToString(),
                                Code = reader["Code"]?.ToString(),
                                Pin = reader["Pin"]?.ToString()
                            });
                        }
                    }
                }
            }

            return resultats;
        }

        public static StockItem ObtenirStockParId(int id)
        {
            using (var connexion = new NpgsqlConnection(GetConnectionString()))
            {
                connexion.Open();
                string requete = "SELECT Id, Code, Price, Pin, Brand, Value FROM stock WHERE Id = @id";

                using (var cmd = new NpgsqlCommand(requete, connexion))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new StockItem
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                Value = reader["Value"].ToString(),
                                Code = reader["Code"].ToString(),
                                Price = reader["Price"].ToString(),
                                Pin = reader["Pin"].ToString(),
                                Brand = reader["Brand"].ToString()
                            };
                        }
                    }
                }
            }

            return null;
        }

        public static bool SupprimerStockParId(int id)
        {
            using (var connexion = new NpgsqlConnection(GetConnectionString()))
            {
                connexion.Open();

                string requete = "DELETE FROM stock WHERE Id = @id";

                using (var cmd = new NpgsqlCommand(requete, connexion))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    int lignesAffectees = cmd.ExecuteNonQuery();

                    return lignesAffectees > 0;
                }
            }
        }



        public static void ChargerUtilisateurs()
        {
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    string requete = "SELECT Id, Achat, Solde, IsBanned, IsAdmin, BanReason FROM users";
                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    using (var reader = cmd.ExecuteReader())
                    {
                        config.UserSave.Clear();
                        config.BanniUser.Clear();
                        config.BanReasons.Clear();
                        config.idAdmins.Clear();
                        while (reader.Read())
                        {
                            long id = reader.GetInt64(0);
                            int achat = reader.GetInt32(1);
                            double solde = reader.GetDouble(2);
                            bool isBanned = !reader.IsDBNull(3) && reader.GetBoolean(3);
                            bool isAdmin = !reader.IsDBNull(4) && reader.GetBoolean(4);
                            string banReason = reader.FieldCount > 5 && !reader.IsDBNull(5) ? reader.GetString(5) : "";

                            config.UserSave.Add(new Tuple<long, int, double, bool>(id, achat, solde, isBanned));
                            if (isBanned)
                            {
                                config.BanniUser.Add(id.ToString());
                                if (!string.IsNullOrEmpty(banReason))
                                {
                                    config.BanReasons[id] = banReason;
                                }
                            }
                            if (isAdmin && !config.idAdmins.Contains(id.ToString()))
                            {
                                config.idAdmins.Add(id.ToString());
                            }
                        }
                    }
                }
            }
            catch
            {

            }
        }

        public static void SauvegarderUtilisateurs()
        {
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    foreach (var item in config.UserSave)
                    {
                        string requete = @"
                        INSERT INTO users (Id, Achat, Solde, IsBanned, BanReason)
                        VALUES (@id, @achat, @solde, @banned, @reason)
                        ON CONFLICT (Id) DO UPDATE SET Achat = EXCLUDED.Achat, Solde = EXCLUDED.Solde, IsBanned = EXCLUDED.IsBanned, BanReason = EXCLUDED.BanReason;";

                        using (var cmd = new NpgsqlCommand(requete, connexion))
                        {
                            cmd.Parameters.AddWithValue("@id", item.Item1);
                            cmd.Parameters.AddWithValue("@achat", item.Item2);
                            cmd.Parameters.AddWithValue("@solde", item.Item3);
                            cmd.Parameters.AddWithValue("@banned", item.Item4);
                            cmd.Parameters.AddWithValue("@reason", config.BanReasons.TryGetValue(item.Item1, out string? r) ? (object)r : DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch
            {

            }
        }

        public static void SauvegarderUtilisateurIndividuel(long userId)
        {
            try
            {
                int index = config.UserSave.FindIndex(u => u.Item1 == userId);
                if (index == -1) return;

                var item = config.UserSave[index];
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    string requete = @"
                    INSERT INTO users (Id, Achat, Solde, IsBanned, BanReason)
                    VALUES (@id, @achat, @solde, @banned, @reason)
                    ON CONFLICT (Id) DO UPDATE SET Achat = EXCLUDED.Achat, Solde = EXCLUDED.Solde, IsBanned = EXCLUDED.IsBanned, BanReason = EXCLUDED.BanReason;";

                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    {
                        cmd.Parameters.AddWithValue("@id", item.Item1);
                        cmd.Parameters.AddWithValue("@achat", item.Item2);
                        cmd.Parameters.AddWithValue("@solde", item.Item3);
                        cmd.Parameters.AddWithValue("@banned", item.Item4);
                        cmd.Parameters.AddWithValue("@reason", config.BanReasons.TryGetValue(item.Item1, out string? r) ? (object)r : DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch
            {

            }
        }





        public static void EnregistrerTransaction(long userId, string brand, string code, string pin, int value, double price)
        {
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    string requete = @"
                    INSERT INTO transactions (UserId, Brand, Code, Pin, Value, Price)
                    VALUES (@userId, @brand, @code, @pin, @value, @price);";

                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@brand", brand ?? "");
                        cmd.Parameters.AddWithValue("@code", code ?? "");
                        cmd.Parameters.AddWithValue("@pin", pin ?? "");
                        cmd.Parameters.AddWithValue("@value", value);
                        cmd.Parameters.AddWithValue("@price", price);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch
            {

            }
        }

        public class TransactionItem
        {
            public int Id { get; set; }
            public long UserId { get; set; }
            public string Brand { get; set; } = "";
            public string Code { get; set; } = "";
            public string Pin { get; set; } = "";
            public int Value { get; set; }
            public double Price { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public static List<TransactionItem> ObtenirTransactions()
        {
            var liste = new List<TransactionItem>();
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    string requete = "SELECT Id, UserId, Brand, Code, Pin, Value, Price, CreatedAt FROM transactions ORDER BY CreatedAt DESC";
                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            liste.Add(new TransactionItem
                            {
                                Id = reader.GetInt32(0),
                                UserId = reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
                                Brand = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Code = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                Pin = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                Value = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                                Price = reader.IsDBNull(6) ? 0.0 : reader.GetDouble(6),
                                CreatedAt = reader.IsDBNull(7) ? DateTime.MinValue : reader.GetDateTime(7)
                            });
                        }
                    }
                }
            }
            catch
            {

            }
            return liste;
        }

        public static void ChargerSettings()
        {
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    string requete = "SELECT Key, Value FROM settings";
                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    using (var reader = cmd.ExecuteReader())
                    {
                        config.CategorySettings.Clear();
                        config.Settings.Clear();
                        while (reader.Read())
                        {
                            string key = reader.GetString(0);
                            string val = reader.GetString(1);
                            config.Settings[key] = val;

                            try
                            {
                                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(val);
                                if (dict != null)
                                {
                                    config.CategorySettings[key] = new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
                                }
                            }
                            catch
                            {
                                if (!config.CategorySettings.ContainsKey("general"))
                                {
                                    config.CategorySettings["general"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                }
                                config.CategorySettings["general"][key] = val;
                            }
                        }
                    }
                }
            }
            catch
            {

            }

            if (!config.CategorySettings.ContainsKey("iptv"))
            {
                config.CategorySettings["iptv"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "api_key", Environment.GetEnvironmentVariable("IPTV_API_KEY") ?? "" },
                    { "api_url", "https://cms-4k.com/api/api.php" },
                    { "pack", "43551" },
                    { "type", "m3u" },
                    { "price_1m", "5" },
                    { "price_3m", "10" },
                    { "price_6m", "15" },
                    { "price_12m", "30" }
                };
                SauvegarderSettings();
            }
            else
            {
                config.CategorySettings["iptv"]["api_key"] = Environment.GetEnvironmentVariable("IPTV_API_KEY") ?? "";
                config.CategorySettings["iptv"]["api_url"] = "https://cms-4k.com/api/api.php";
                config.CategorySettings["iptv"]["pack"] = "43551";
                config.CategorySettings["iptv"]["type"] = "m3u";
                if (!config.CategorySettings["iptv"].ContainsKey("price_1m")) config.CategorySettings["iptv"]["price_1m"] = "5";
                if (!config.CategorySettings["iptv"].ContainsKey("price_3m")) config.CategorySettings["iptv"]["price_3m"] = "10";
                if (!config.CategorySettings["iptv"].ContainsKey("price_6m")) config.CategorySettings["iptv"]["price_6m"] = "15";
                if (!config.CategorySettings["iptv"].ContainsKey("price_12m")) config.CategorySettings["iptv"]["price_12m"] = "30";
                SauvegarderSettings();
            }

            if (!config.CategorySettings.ContainsKey("metrics"))
            {
                config.PersisterMetricsInSettings();
                SauvegarderSettings();
            }
            else
            {
                config.ChargerMetricsFromSettings();
            }
        }

        public static void SauvegarderSettings()
        {
            try
            {
                config.PersisterMetricsInSettings();
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    foreach (var cat in config.CategorySettings)
                    {
                        string jsonValue = JsonSerializer.Serialize(cat.Value);
                        string requete = @"
                        INSERT INTO settings (Key, Value)
                        VALUES (@k, @v::jsonb)
                        ON CONFLICT (Key) DO UPDATE SET Value = EXCLUDED.Value;";

                        using (var cmd = new NpgsqlCommand(requete, connexion))
                        {
                            cmd.Parameters.AddWithValue("@k", cat.Key);
                            cmd.Parameters.AddWithValue("@v", jsonValue);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch
            {

            }
        }

        public class PaymentRecord
        {
            public int Id { get; set; }
            public string ChatId { get; set; } = "";
            public string TrackId { get; set; } = "";
            public double Amount { get; set; }
            public string PaymentMethod { get; set; } = "";
            public string Status { get; set; } = "";
            public string PaymentUrl { get; set; } = "";
            public DateTime CreatedAt { get; set; }
        }

        public static bool CreerPaiementEnBDD(string chatId, string trackId, double amount, string paymentMethod, string paymentUrl)
        {
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    string requete = @"
                    INSERT INTO payments (ChatId, TrackId, Amount, PaymentMethod, Status, PaymentUrl)
                    VALUES (@chatId, @trackId, @amount, @method, 'PENDING', @url)
                    ON CONFLICT (TrackId) DO UPDATE SET Status = 'PENDING', Amount = EXCLUDED.Amount;";

                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    {
                        cmd.Parameters.AddWithValue("@chatId", chatId);
                        cmd.Parameters.AddWithValue("@trackId", trackId);
                        cmd.Parameters.AddWithValue("@amount", amount);
                        cmd.Parameters.AddWithValue("@method", paymentMethod);
                        cmd.Parameters.AddWithValue("@url", paymentUrl);
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static List<PaymentRecord> ObtenirPaiementsEnAttenteBDD(string paymentMethod)
        {
            var list = new List<PaymentRecord>();
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    string requete = "SELECT Id, ChatId, TrackId, Amount, PaymentMethod, Status, PaymentUrl, CreatedAt FROM payments WHERE PaymentMethod = @method AND (Status = 'PENDING' OR (Status = 'FAILED' AND CreatedAt > NOW() - INTERVAL '20 minutes') OR (Status = 'EXPIRED' AND CreatedAt > NOW() - INTERVAL '2 hours'))";
                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    {
                        cmd.Parameters.AddWithValue("@method", paymentMethod);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                list.Add(new PaymentRecord
                                {
                                    Id = reader.GetInt32(0),
                                    ChatId = reader.GetString(1),
                                    TrackId = reader.GetString(2),
                                    Amount = reader.GetDouble(3),
                                    PaymentMethod = reader.GetString(4),
                                    Status = reader.GetString(5),
                                    PaymentUrl = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    CreatedAt = reader.IsDBNull(7) ? DateTime.UtcNow : reader.GetDateTime(7)
                                });
                            }
                        }
                    }
                }
            }
            catch
            {

            }
            return list;
        }

        public static bool MettreAJourPaiementStatutBDD(string trackId, string status)
        {
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    string requete = "UPDATE payments SET Status = @status WHERE TrackId = @trackId";
                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    {
                        cmd.Parameters.AddWithValue("@status", status);
                        cmd.Parameters.AddWithValue("@trackId", trackId);
                        int rows = cmd.ExecuteNonQuery();
                        return rows > 0;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public static string ObtenirStatutPaiementBDD(string trackId)
        {
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    string requete = "SELECT Status FROM payments WHERE TrackId = @trackId";
                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    {
                        cmd.Parameters.AddWithValue("@trackId", trackId);
                        var obj = cmd.ExecuteScalar();
                        return obj != null ? obj.ToString() ?? "" : "";
                    }
                }
            }
            catch
            {
                return "";
            }
        }

        public static string ObtenirUrlPaiementBDD(string trackId)
        {
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    string requete = "SELECT PaymentUrl FROM payments WHERE TrackId = @trackId";
                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    {
                        cmd.Parameters.AddWithValue("@trackId", trackId);
                        var obj = cmd.ExecuteScalar();
                        return obj != null ? obj.ToString() ?? "" : "";
                    }
                }
            }
            catch
            {
                return "";
            }
        }

        public static bool AUnPaiementEnAttenteBDD(string chatId)
        {
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();

                    string reqClean = "UPDATE payments SET Status = 'EXPIRED' WHERE Status = 'PENDING' AND CreatedAt < NOW() - INTERVAL '30 minutes'";
                    using (var cmdClean = new NpgsqlCommand(reqClean, connexion))
                    {
                        cmdClean.ExecuteNonQuery();
                    }

                    string requete = "SELECT COUNT(1) FROM payments WHERE ChatId = @chatId AND Status = 'PENDING'";
                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    {
                        cmd.Parameters.AddWithValue("@chatId", chatId);
                        long count = (long)cmd.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool AnnulerPaiementEnAttenteBDD(string chatId)
        {
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    string requete = "UPDATE payments SET Status = 'CANCELED' WHERE ChatId = @chatId AND Status = 'PENDING'";
                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    {
                        cmd.Parameters.AddWithValue("@chatId", chatId);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public static PaymentRecord? ObtenirPaiementParTrackIdBDD(string trackId)
        {
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    string requete = "SELECT Id, ChatId, TrackId, Amount, PaymentMethod, Status, PaymentUrl, CreatedAt FROM payments WHERE TrackId = @trackId";
                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    {
                        cmd.Parameters.AddWithValue("@trackId", trackId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new PaymentRecord
                                {
                                    Id = reader.GetInt32(0),
                                    ChatId = reader.GetString(1),
                                    TrackId = reader.GetString(2),
                                    Amount = reader.GetDouble(3),
                                    PaymentMethod = reader.GetString(4),
                                    Status = reader.GetString(5),
                                    PaymentUrl = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    CreatedAt = reader.IsDBNull(7) ? DateTime.UtcNow : reader.GetDateTime(7)
                                };
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public static List<PaymentRecord> ObtenirTousLesPaiementsBDD()
        {
            var list = new List<PaymentRecord>();
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    string requete = "SELECT Id, ChatId, TrackId, Amount, PaymentMethod, Status, PaymentUrl, CreatedAt FROM payments ORDER BY CreatedAt DESC";
                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new PaymentRecord
                            {
                                Id = reader.GetInt32(0),
                                ChatId = reader.GetString(1),
                                TrackId = reader.GetString(2),
                                Amount = reader.GetDouble(3),
                                PaymentMethod = reader.GetString(4),
                                Status = reader.GetString(5),
                                PaymentUrl = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                CreatedAt = reader.IsDBNull(7) ? DateTime.UtcNow : reader.GetDateTime(7)
                            });
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        public static DateTime ConvertirEnHeureParis(DateTime dateUtc)
        {
            try
            {
                TimeZoneInfo tzParis = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
                DateTime utc = dateUtc.Kind == DateTimeKind.Utc ? dateUtc : DateTime.SpecifyKind(dateUtc, DateTimeKind.Utc);
                return TimeZoneInfo.ConvertTimeFromUtc(utc, tzParis);
            }
            catch
            {
                try
                {
                    TimeZoneInfo tzParisWin = TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
                    DateTime utc = dateUtc.Kind == DateTimeKind.Utc ? dateUtc : DateTime.SpecifyKind(dateUtc, DateTimeKind.Utc);
                    return TimeZoneInfo.ConvertTimeFromUtc(utc, tzParisWin);
                }
                catch
                {
                    int month = dateUtc.Month;
                    bool isWinter = month <= 3 || month >= 11;
                    return dateUtc.AddHours(isWinter ? 1 : 2);
                }
            }
        }
    }
}
