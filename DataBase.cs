using Npgsql;

namespace UgcBotTG
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
            string dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL") ?? Environment.GetEnvironmentVariable("POSTGRES_URL") ?? "";
            if (!string.IsNullOrEmpty(dbUrl))
            {
                try
                {
                    Uri uri = new Uri(dbUrl);
                    string userInfo = uri.UserInfo;
                    string[] userParts = userInfo.Split(':');
                    string username = userParts[0];
                    string password = userParts.Length > 1 ? userParts[1] : "";
                    string host = uri.Host;
                    int port = uri.Port > 0 ? uri.Port : 5432;
                    string database = uri.AbsolutePath.TrimStart('/');

                    return $"Host={host};Port={port};Username={username};Password={password};Database={database};SslMode=Prefer;";
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

            return $"Host={h};Port={pt};Username={u};Password={p};Database={d};";
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
                    Solde DOUBLE PRECISION DEFAULT 0.0
                );
                CREATE TABLE IF NOT EXISTS bans (
                    ChatId TEXT PRIMARY KEY
                );
                CREATE TABLE IF NOT EXISTS parrainage (
                    Filleul TEXT PRIMARY KEY,
                    Parrain TEXT
                );
                CREATE TABLE IF NOT EXISTS profile (
                    Key TEXT PRIMARY KEY,
                    Value TEXT
                );";

                using (var commande = new NpgsqlCommand(requete, connexion))
                {
                    commande.ExecuteNonQuery();
                }
            }
        }

        public static void LireEtInsererDepuisFichier(string cheminFichier)
        {
            if (!File.Exists(cheminFichier))
            {
                return;
            }

            foreach (var ligne in File.ReadAllLines(cheminFichier))
            {
                string[] parts = ligne.Split('|');
                if (parts.Length == 5)
                {
                    string brand = parts[0];
                    string code = parts[1];
                    string pin = parts[2];
                    int value = int.Parse(parts[3]);
                    double price = Double.Parse(parts[4]);

                    InsererDansStock(brand, code, pin, value, price);
                }
                else if (parts.Length == 4)
                {
                    string brand = parts[0];
                    string code = parts[1];
                    string pin = "";
                    int value = int.Parse(parts[2]);
                    double price = double.Parse(parts[3]);

                    InsererDansStock(brand, code, pin, value, price);
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

        public static List<StockItem> ObtenirStocksParBrand(string brand)
        {
            var resultats = new List<StockItem>();

            using (var connexion = new NpgsqlConnection(GetConnectionString()))
            {
                connexion.Open();
                string requete = "SELECT Id, Value, Price FROM stock WHERE Brand = @brand";

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
                                Price = reader["Price"].ToString()
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

        public static bool SupprimerStockParBrand(string brand)
        {
            using (var connexion = new NpgsqlConnection(GetConnectionString()))
            {
                connexion.Open();

                string requete = "DELETE FROM stock WHERE Brand = @brand";

                using (var cmd = new NpgsqlCommand(requete, connexion))
                {
                    cmd.Parameters.AddWithValue("@brand", brand);
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
                    string requete = "SELECT Id, Achat, Solde FROM users";
                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    using (var reader = cmd.ExecuteReader())
                    {
                        config.UserSave.Clear();
                        while (reader.Read())
                        {
                            long id = reader.GetInt64(0);
                            int achat = reader.GetInt32(1);
                            double solde = reader.GetDouble(2);
                            config.UserSave.Add(new Tuple<long, int, double>(id, achat, solde));
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
                        INSERT INTO users (Id, Achat, Solde)
                        VALUES (@id, @achat, @solde)
                        ON CONFLICT (Id) DO UPDATE SET Achat = EXCLUDED.Achat, Solde = EXCLUDED.Solde;";

                        using (var cmd = new NpgsqlCommand(requete, connexion))
                        {
                            cmd.Parameters.AddWithValue("@id", item.Item1);
                            cmd.Parameters.AddWithValue("@achat", item.Item2);
                            cmd.Parameters.AddWithValue("@solde", item.Item3);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch
            {

            }
        }

        public static void ChargerBannis()
        {
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    string requete = "SELECT ChatId FROM bans";
                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    using (var reader = cmd.ExecuteReader())
                    {
                        config.BanniUser.Clear();
                        while (reader.Read())
                        {
                            config.BanniUser.Add(reader.GetString(0));
                        }
                    }
                }
            }
            catch
            {

            }
        }

        public static void SauvegarderBannis()
        {
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    using (var cmdDel = new NpgsqlCommand("TRUNCATE TABLE bans", connexion))
                    {
                        cmdDel.ExecuteNonQuery();
                    }

                    foreach (var id in config.BanniUser)
                    {
                        using (var cmd = new NpgsqlCommand("INSERT INTO bans (ChatId) VALUES (@id) ON CONFLICT DO NOTHING", connexion))
                        {
                            cmd.Parameters.AddWithValue("@id", id);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch
            {

            }
        }

        public static void ChargerParrains()
        {
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    string requete = "SELECT Filleul, Parrain FROM parrainage";
                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    using (var reader = cmd.ExecuteReader())
                    {
                        config.ParainUser.Clear();
                        while (reader.Read())
                        {
                            config.ParainUser[reader.GetString(0)] = reader.GetString(1);
                        }
                    }
                }
            }
            catch
            {

            }
        }

        public static void SauvegarderParrains()
        {
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    foreach (var item in config.ParainUser)
                    {
                        string requete = @"
                        INSERT INTO parrainage (Filleul, Parrain)
                        VALUES (@f, @p)
                        ON CONFLICT (Filleul) DO UPDATE SET Parrain = EXCLUDED.Parrain;";

                        using (var cmd = new NpgsqlCommand(requete, connexion))
                        {
                            cmd.Parameters.AddWithValue("@f", item.Key);
                            cmd.Parameters.AddWithValue("@p", item.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch
            {

            }
        }

        public static void ChargerProfile()
        {
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    string requete = "SELECT Key, Value FROM profile";
                    using (var cmd = new NpgsqlCommand(requete, connexion))
                    using (var reader = cmd.ExecuteReader())
                    {
                        config.ProfileSettings.Clear();
                        while (reader.Read())
                        {
                            config.ProfileSettings[reader.GetString(0)] = reader.GetString(1);
                        }
                    }
                }
            }
            catch
            {

            }
        }

        public static void SauvegarderProfile()
        {
            try
            {
                using (var connexion = new NpgsqlConnection(GetConnectionString()))
                {
                    connexion.Open();
                    foreach (var item in config.ProfileSettings)
                    {
                        string requete = @"
                        INSERT INTO profile (Key, Value)
                        VALUES (@k, @v)
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
            catch
            {

            }
        }
    }
}
