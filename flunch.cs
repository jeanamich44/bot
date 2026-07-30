using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChezRheyyBot
{
    internal class flunch
    {
        public static async Task<bool> GenererNouveauxStockFlunch()
        {
            try
            {
                string url = "https://rheyy-services.up.railway.app/generate-flunch-list?count=50";
                string outputFile = "data.txt";
                File.WriteAllText(outputFile, string.Empty);
                List<string> goodLines = new List<string>();

                int count = 0;

                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(1);

                    try
                    {
                        //Console.WriteLine("Requête en cours...");

                        do
                        {
                            HttpResponseMessage response = await client.GetAsync(url);
                            response.EnsureSuccessStatusCode();

                            string responseBody = await response.Content.ReadAsStringAsync();

                            string[] lines = responseBody.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                            count++;
                           

                            foreach (var line in lines)
                            {
                                string[] parts = line.Split('|');

                                if (parts.Length >= 3)
                                {
                                    if (int.TryParse(parts[2], out int value))
                                    {
                                        if (value >= 150)
                                        {
                                            goodLines.Add(line);
                                        }
                                    }
                                }
                            }

                            if(count > 5)
                            {
                                break;
                            }
                        } while (goodLines.Count < 10);

                        await File.WriteAllLinesAsync(outputFile, goodLines);

                        Console.WriteLine("Nouveaux stock flunch pret !!");



                        Console.WriteLine("Vidage de flunch en cours !!");
                        if (!DataBase.SupprimerStockParBrand("flunch"))
                        {
                            Console.WriteLine("Erreur: Impossible de supprimer le stock flunch !!");
                            return false;
                        }


                        Console.WriteLine("Stock flunch supprimer avec success !!");


                        string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;

                        // Nom du fichier à lancer
                        string exeName = "main.exe";

                        // Chemin complet vers l'exe
                        string exePath = Path.Combine(currentDirectory, exeName);

                        // Vérifie si le fichier existe
                        if (File.Exists(exePath))
                        {
                            Process.Start(exePath);
                            Console.WriteLine("Programme lancé !");
                        }
                        else
                        {
                            Console.WriteLine("Fichier introuvable : " + exePath);
                        }

                        return true;

                        //Console.WriteLine($"Terminé. {goodLines.Count} lignes sauvegardées dans {outputFile}");
                    }
                    catch (TaskCanceledException)
                    {
                        //Console.WriteLine("Timeout dépassé.");
                        return false;
                    }
                    catch (Exception ex)
                    {
                        //Console.WriteLine("Erreur: " + ex.Message);
                        return false;
                    }
                }
            }
            catch
            {

            }

            return false;
        }
    }
}
