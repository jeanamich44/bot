using Npgsql;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;

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
            string dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL") ?? "";
            if (string.IsNullOrWhiteSpace(dbUrl))
                throw new InvalidOperationException("Variable d'environnement DATABASE_URL manquante.");

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
                ALTER TABLE users ADD COLUMN IF NOT EXISTS BanReason TEXT;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS UserNumber INTEGER;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS Username TEXT;
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
                string requete = "SELECT Id, Value, Price, Code, Pin FROM stock WHERE Brand = @brand ORDER BY Value DESC, Price DESC;";

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

            return resultats.OrderByDescending(x => int.TryParse(x.Value, out int v) ? v : 0)
                            .ThenByDescending(x => double.TryParse(x.Price, out double p) ? p : 0)
                            .ToList();
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

                    string requete = "SELECT Id, Achat, Solde, IsBanned, IsAdmin, BanReason, UserNumber, Username FROM users";
                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var newUsers = new List<Utilisateur>();
                        var newBanned = new List<string>();
                        var newReasons = new Dictionary<long, string>();
                        var newAdmins = new List<string>();
                        var newNames = new Dictionary<long, string>();
                        var newNumbers = new Dictionary<long, int>();

                        while (reader.Read())
                        {
                            long id = reader.GetInt64(0);
                            int achat = reader.GetInt32(1);
                            double solde = reader.GetDouble(2);
                            bool isBanned = !reader.IsDBNull(3) && reader.GetBoolean(3);
                            bool isAdmin = !reader.IsDBNull(4) && reader.GetBoolean(4);
                            string banReason = reader.FieldCount > 5 && !reader.IsDBNull(5) ? reader.GetString(5) : "";
                            int userNum = reader.FieldCount > 6 && !reader.IsDBNull(6) ? reader.GetInt32(6) : 0;
                            string uname = reader.FieldCount > 7 && !reader.IsDBNull(7) ? reader.GetString(7) : "";

                            if (!string.IsNullOrWhiteSpace(uname))
                            {
                                newNames[id] = uname;
                            }

                            if (userNum > 0)
                            {
                                newNumbers[id] = userNum;
                            }

                            newUsers.Add(new Utilisateur { Id = id, Achat = achat, Solde = solde, IsBanned = isBanned });
                            if (isBanned)
                            {
                                newBanned.Add(id.ToString());
                                if (!string.IsNullOrEmpty(banReason))
                                {
                                    newReasons[id] = banReason;
                                }
                            }
                            if (isAdmin && !newAdmins.Contains(id.ToString()))
                            {
                                newAdmins.Add(id.ToString());
                            }
                        }

                        lock (config.UsersLock)
                        {
                            config.UserSave = newUsers;
                            config.BanniUser = newBanned;
                            config.BanReasons = newReasons;
                            config.idAdmins = newAdmins;
                            config.Usernames = newNames;
                            foreach (var kv in newNumbers) config.UserNumbers[kv.Key] = kv.Value;
                            foreach (var u in newUsers)
                            {
                                if (!config.UserNumbers.ContainsKey(u.Id))
                                {
                                    config.ObtenirOuCreerNumeroUtilisateur(u.Id);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChargerUtilisateurs Erreur] {ex.Message}");
            }
        }

        public static void SauvegarderUtilisateurs()
        {
            try
            {
                var copie = config.CopierUtilisateurs();
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    foreach (var item in copie)
                    {
                        string requete = @"
                        INSERT INTO users (Id, Achat, Solde, IsBanned, BanReason, UserNumber, Username, IsAdmin)
                        VALUES (@id, @achat, @solde, @banned, @reason, @usernum, @username, @isadmin)
                        ON CONFLICT (Id) DO UPDATE SET Achat = EXCLUDED.Achat, Solde = EXCLUDED.Solde, IsBanned = EXCLUDED.IsBanned, BanReason = EXCLUDED.BanReason, UserNumber = EXCLUDED.UserNumber, Username = EXCLUDED.Username, IsAdmin = EXCLUDED.IsAdmin;";

                        using (var cmd = new NpgsqlCommand(requete, connexion))
                        {
                            cmd.Parameters.AddWithValue("@id", item.Id);
                            cmd.Parameters.AddWithValue("@achat", item.Achat);
                            cmd.Parameters.AddWithValue("@solde", item.Solde);
                            cmd.Parameters.AddWithValue("@banned", item.IsBanned);
                            cmd.Parameters.AddWithValue("@reason", config.BanReasons.TryGetValue(item.Id, out string? r) ? (object)r : DBNull.Value);
                            cmd.Parameters.AddWithValue("@usernum", config.ObtenirOuCreerNumeroUtilisateur(item.Id));
                            cmd.Parameters.AddWithValue("@username", config.Usernames.TryGetValue(item.Id, out string? u) && !string.IsNullOrWhiteSpace(u) ? (object)u : DBNull.Value);
                            cmd.Parameters.AddWithValue("@isadmin", config.idAdmins.Contains(item.Id.ToString()));
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SauvegarderUtilisateurs Erreur] {ex.Message}");
            }
        }

        public static void SauvegarderUtilisateurIndividuel(long userId)
        {
            try
            {
                var item = config.TrouverUtilisateur(userId);
                if (item == null) return;
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    string requete = @"
                    INSERT INTO users (Id, Achat, Solde, IsBanned, BanReason, UserNumber, Username, IsAdmin)
                    VALUES (@id, @achat, @solde, @banned, @reason, @usernum, @username, @isadmin)
                    ON CONFLICT (Id) DO UPDATE SET Achat = EXCLUDED.Achat, Solde = EXCLUDED.Solde, IsBanned = EXCLUDED.IsBanned, BanReason = EXCLUDED.BanReason, UserNumber = EXCLUDED.UserNumber, Username = EXCLUDED.Username, IsAdmin = EXCLUDED.IsAdmin;";

                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    {
                        cmd.Parameters.AddWithValue("@id", item.Id);
                        cmd.Parameters.AddWithValue("@achat", item.Achat);
                        cmd.Parameters.AddWithValue("@solde", item.Solde);
                        cmd.Parameters.AddWithValue("@banned", item.IsBanned);
                        cmd.Parameters.AddWithValue("@reason", config.BanReasons.TryGetValue(item.Id, out string? r) ? (object)r : DBNull.Value);
                        cmd.Parameters.AddWithValue("@usernum", config.ObtenirOuCreerNumeroUtilisateur(item.Id));
                        cmd.Parameters.AddWithValue("@username", config.Usernames.TryGetValue(item.Id, out string? u) && !string.IsNullOrWhiteSpace(u) ? (object)u : DBNull.Value);
                        cmd.Parameters.AddWithValue("@isadmin", config.idAdmins.Contains(item.Id.ToString()));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SauvegarderUtilisateurIndividuel Erreur] {ex.Message}");
            }
        }

        public static bool SupprimerUtilisateurCompletBDD(long userId)
        {
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    using var cmd = new NpgsqlCommand("DELETE FROM users WHERE Id = @id", connexion);
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.ExecuteNonQuery();
                }
                lock (config.UsersLock)
                {
                    config.UserSave.RemoveAll(u => u.Id == userId);
                    config.Usernames.Remove(userId);
                    config.UserNumbers.Remove(userId);
                    config.BanReasons.Remove(userId);
                    config.BanniUser.Remove(userId.ToString());
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task SynchroniserUsernamesTelegram(Telegram.Bot.ITelegramBotClient botClient)
        {
            var usersCopy = config.UserSave.ToList();
            foreach (var u in usersCopy)
            {
                long userId = u.Id;
                if (!config.Usernames.TryGetValue(userId, out string? existing) || string.IsNullOrWhiteSpace(existing))
                {
                    try
                    {
                        var chat = await botClient.GetChatAsync(new ChatId(userId));
                        if (chat != null && !string.IsNullOrWhiteSpace(chat.Username))
                        {
                            string formatted = chat.Username.StartsWith("@") ? chat.Username : "@" + chat.Username;
                            config.Usernames[userId] = formatted;
                            SauvegarderUtilisateurIndividuel(userId);
                        }
                    }
                    catch { }
                }
            }
        }





        public static bool AcheterStockAtomique(long userId, int stockId, out StockItem? item, out double nouveauSolde, out int nouveauxAchats)
        {
            item = null;
            nouveauSolde = 0;
            nouveauxAchats = 0;

            using var connexion = new NpgsqlConnection(GetConnectionString());
            connexion.Open();
            using var tx = connexion.BeginTransaction();
            try
            {
                StockItem stock;
                using (var cmd = new NpgsqlCommand("SELECT Id, Brand, Code, Pin, Value, Price FROM stock WHERE Id = @id FOR UPDATE", connexion, tx))
                {
                    cmd.Parameters.AddWithValue("@id", stockId);
                    using var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                    {
                        tx.Rollback();
                        return false;
                    }

                    stock = new StockItem
                    {
                        Id = reader.GetInt32(0),
                        Brand = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Code = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Pin = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        Value = reader.IsDBNull(4) ? "0" : reader.GetInt32(4).ToString(),
                        Price = reader.IsDBNull(5) ? "0" : reader.GetDouble(5).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    };
                }

                double.TryParse((stock.Price ?? "0").Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double prix);

                using (var cmd = new NpgsqlCommand(@"
                    INSERT INTO users (Id, Achat, Solde, IsBanned) VALUES (@id, 0, 0, FALSE)
                    ON CONFLICT (Id) DO NOTHING;
                    SELECT Solde FROM users WHERE Id = @id FOR UPDATE;", connexion, tx))
                {
                    cmd.Parameters.AddWithValue("@id", userId);
                    object? soldeObj = cmd.ExecuteScalar();
                    double solde = soldeObj == null || soldeObj is DBNull ? 0 : Convert.ToDouble(soldeObj);
                    if (solde < prix)
                    {
                        tx.Rollback();
                        return false;
                    }
                }

                using (var cmd = new NpgsqlCommand("UPDATE users SET Solde = Solde - @p, Achat = Achat + 1 WHERE Id = @id RETURNING Solde, Achat", connexion, tx))
                {
                    cmd.Parameters.AddWithValue("@p", prix);
                    cmd.Parameters.AddWithValue("@id", userId);
                    using var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                    {
                        tx.Rollback();
                        return false;
                    }
                    nouveauSolde = reader.GetDouble(0);
                    nouveauxAchats = reader.GetInt32(1);
                }

                using (var cmd = new NpgsqlCommand("DELETE FROM stock WHERE Id = @id", connexion, tx))
                {
                    cmd.Parameters.AddWithValue("@id", stockId);
                    cmd.ExecuteNonQuery();
                }

                int.TryParse(stock.Value, out int val);
                using (var cmd = new NpgsqlCommand(@"
                    INSERT INTO transactions (UserId, Brand, Code, Pin, Value, Price)
                    VALUES (@userId, @brand, @code, @pin, @value, @price);", connexion, tx))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@brand", stock.Brand ?? "");
                    cmd.Parameters.AddWithValue("@code", stock.Code ?? "");
                    cmd.Parameters.AddWithValue("@pin", (object?)stock.Pin ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@value", val > 0 ? val : DBNull.Value);
                    cmd.Parameters.AddWithValue("@price", prix);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                item = stock;
                var cached = config.TrouverUtilisateur(userId);
                config.SynchroniserCacheUtilisateur(userId, nouveauxAchats, nouveauSolde, cached?.IsBanned ?? false);
                return true;
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { }
                Console.WriteLine($"[AcheterStockAtomique Erreur] {ex.Message}");
                item = null;
                return false;
            }
        }

        public static bool DebiterSoldeAtomique(long userId, double montant, bool incrementerAchat, out double nouveauSolde)
        {
            nouveauSolde = 0;
            if (montant < 0) return false;

            using var connexion = new NpgsqlConnection(GetConnectionString());
            connexion.Open();
            using var tx = connexion.BeginTransaction();
            try
            {
                using (var cmd = new NpgsqlCommand(@"
                    INSERT INTO users (Id, Achat, Solde, IsBanned) VALUES (@id, 0, 0, FALSE)
                    ON CONFLICT (Id) DO NOTHING;", connexion, tx))
                {
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.ExecuteNonQuery();
                }

                string sql = incrementerAchat
                    ? "UPDATE users SET Solde = Solde - @p, Achat = Achat + 1 WHERE Id = @id AND Solde >= @p RETURNING Solde, Achat, IsBanned"
                    : "UPDATE users SET Solde = Solde - @p WHERE Id = @id AND Solde >= @p RETURNING Solde, Achat, IsBanned";

                using (var cmd = new NpgsqlCommand(sql, connexion, tx))
                {
                    cmd.Parameters.AddWithValue("@p", montant);
                    cmd.Parameters.AddWithValue("@id", userId);
                    using var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                    {
                        tx.Rollback();
                        return false;
                    }
                    nouveauSolde = reader.GetDouble(0);
                    int achats = reader.GetInt32(1);
                    bool banned = !reader.IsDBNull(2) && reader.GetBoolean(2);
                    reader.Close();
                    tx.Commit();
                    config.SynchroniserCacheUtilisateur(userId, achats, nouveauSolde, banned);
                    return true;
                }
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { }
                Console.WriteLine($"[DebiterSoldeAtomique Erreur] {ex.Message}");
                return false;
            }
        }

        public static double CrediterSoldeAtomique(long userId, double montant)
        {
            using var connexion = new NpgsqlConnection(GetConnectionString());
            connexion.Open();
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO users (Id, Achat, Solde, IsBanned) VALUES (@id, 0, @m, FALSE)
                ON CONFLICT (Id) DO UPDATE SET Solde = users.Solde + @m
                RETURNING Solde, Achat, IsBanned;", connexion);
            cmd.Parameters.AddWithValue("@id", userId);
            cmd.Parameters.AddWithValue("@m", montant);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return 0;
            double solde = reader.GetDouble(0);
            int achats = reader.GetInt32(1);
            bool banned = !reader.IsDBNull(2) && reader.GetBoolean(2);
            config.SynchroniserCacheUtilisateur(userId, achats, solde, banned);
            return solde;
        }

        public static void EnregistrerTransaction(long userId, string brand, string code, string pin, int? value, double price)
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
                        if (value.HasValue && value.Value > 0)
                            cmd.Parameters.AddWithValue("@value", value.Value);
                        else
                            cmd.Parameters.AddWithValue("@value", DBNull.Value);
                        cmd.Parameters.AddWithValue("@price", price);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EnregistrerTransaction Erreur] {ex.Message}");
            }
        }

        public class TransactionItem
        {
            public int Id { get; set; }
            public long UserId { get; set; }
            public string Brand { get; set; } = "";
            public string Code { get; set; } = "";
            public string Pin { get; set; } = "";
            public int? Value { get; set; }
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
                                Value = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
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
            bool dbSuccess = false;
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    string requete = "SELECT Key, Value FROM settings";
                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var tempSettings = new Dictionary<string, string>();
                        var tempCategorySettings = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

                        while (reader.Read())
                        {
                            string key = reader.GetString(0);
                            string val = reader.GetString(1);
                            tempSettings[key] = val;

                            try
                            {
                                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(val);
                                if (dict != null)
                                {
                                    tempCategorySettings[key] = new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
                                }
                            }
                            catch
                            {
                                if (!tempCategorySettings.ContainsKey("general"))
                                {
                                    tempCategorySettings["general"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                }
                                tempCategorySettings["general"][key] = val;
                            }
                        }

                        lock (config.SettingsLock)
                        {
                            foreach (var kvp in tempSettings) config.Settings[kvp.Key] = kvp.Value;
                            foreach (var kvp in tempCategorySettings) config.CategorySettings[kvp.Key] = kvp.Value;
                        }
                        dbSuccess = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChargerSettings Erreur] {ex.Message}");
            }

            lock (config.SettingsLock)
            {
                if (!config.CategorySettings.ContainsKey("iptv"))
                {
                    config.CategorySettings["iptv"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                var iptvDict = config.CategorySettings["iptv"];

                if (!iptvDict.ContainsKey("host") || string.IsNullOrWhiteSpace(iptvDict["host"]))
                    iptvDict["host"] = "http://cf.business-cloud-neo.com";

                if (!iptvDict.ContainsKey("message_footer") || string.IsNullOrWhiteSpace(iptvDict["message_footer"]))
                    iptvDict["message_footer"] = "Afin d'installer facilement les meilleures applications IPTV, voici le meilleur tuto avec toutes les applications pour n'importe quel appareil :\nhttps://neo4k.fr/guide-dinstallation-iptv-france/";

                bool hasAccounts = iptvDict.TryGetValue("accounts", out string? accountsRaw)
                    && !string.IsNullOrWhiteSpace(accountsRaw)
                    && accountsRaw.TrimStart().StartsWith("[");
                if (!hasAccounts)
                {
                    string legacyKey = iptvDict.TryGetValue("api_key", out string? k) ? k : "";
                    string legacyUrl = iptvDict.TryGetValue("api_url", out string? u) ? u : "";
                    string legacyPack = iptvDict.TryGetValue("pack", out string? p) ? p : "";
                    if (!string.IsNullOrWhiteSpace(legacyKey) && !string.IsNullOrWhiteSpace(legacyPack))
                    {
                        var migrated = new List<iptv.IptvAccount>
                        {
                            new iptv.IptvAccount { Name = "Compte 1", ApiKey = legacyKey, ApiUrl = legacyUrl ?? "", Pack = legacyPack }
                        };
                        iptvDict["accounts"] = JsonSerializer.Serialize(migrated);
                    }
                    else if (!iptvDict.ContainsKey("accounts"))
                    {
                        iptvDict["accounts"] = "[]";
                    }
                }
            }

            if (dbSuccess)
            {
                config.ChargerMetricsFromSettings();
            }

            SauvegarderSettings();
        }

        public static void SauvegarderSettings()
        {
            try
            {
                config.PersisterMetricsInSettings();

                List<KeyValuePair<string, string>> snapshot;
                lock (config.SettingsLock)
                {
                    snapshot = config.CategorySettings.Select(cat => new KeyValuePair<string, string>(cat.Key, JsonSerializer.Serialize(cat.Value))).ToList();
                }

                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    foreach (var item in snapshot)
                    {
                        string requete = @"
                        INSERT INTO settings (Key, Value)
                        VALUES (@k, @v::jsonb)
                        ON CONFLICT (Key) DO UPDATE SET Value = EXCLUDED.Value;";

                        using (var cmd = new NpgsqlCommand(requete, connexion))
                        {
                            cmd.Parameters.AddWithValue("@k", item.Key);
                            cmd.Parameters.AddWithValue("@v", item.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SauvegarderSettings Erreur] {ex.Message}");
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
                    string requete = "SELECT Id, ChatId, TrackId, Amount, PaymentMethod, Status, PaymentUrl, CreatedAt FROM payments WHERE PaymentMethod = @method AND (Status = 'PENDING' OR (Status = 'FAILED' AND CreatedAt > NOW() - INTERVAL '20 minutes') OR (Status = 'EXPIRED' AND CreatedAt > NOW() - INTERVAL '2 hours') OR (Status = 'CANCELED' AND CreatedAt > NOW() - INTERVAL '35 minutes'))";
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
                        var result = cmd.ExecuteScalar();
                        long count = result != null && result != DBNull.Value ? Convert.ToInt64(result) : 0;
                        return count > 0;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public static PaymentRecord? ObtenirPaiementEnAttenteParChatIdBDD(string chatId)
        {
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    string requete = "SELECT Id, ChatId, TrackId, Amount, PaymentMethod, Status, PaymentUrl, CreatedAt FROM payments WHERE ChatId = @chatId AND Status = 'PENDING' LIMIT 1";
                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    {
                        cmd.Parameters.AddWithValue("@chatId", chatId);
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
            catch
            {
            }
            return null;
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

        public static int ViderStockBDD(string brand)
        {
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    string requete = string.Equals(brand, "all", StringComparison.OrdinalIgnoreCase)
                        ? "DELETE FROM stock;"
                        : "DELETE FROM stock WHERE LOWER(brand) = LOWER(@brand);";

                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    {
                        if (!string.Equals(brand, "all", StringComparison.OrdinalIgnoreCase))
                        {
                            cmd.Parameters.AddWithValue("@brand", brand);
                        }
                        return cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ViderStockBDD Erreur] {ex.Message}");
                return 0;
            }
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
