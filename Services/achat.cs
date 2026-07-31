using System.Data.Common;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace ChezRheyyBot
{
    internal class achat
    {
        public static async Task DemandeAchat(string message, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var msg = update.CallbackQuery.Data.Split('_');

                int result = config.UserSave.FindIndex(tuple => tuple.Item1 == long.Parse(config.CurrentChatId));

                if (result == -1)
                {
                    return;
                }

                var ancienTuple = config.UserSave[result];

                var item = DataBase.ObtenirStockParId(int.Parse(msg[1]));

                if (item != null)
                {
                    var prix = item.Price;
                    var solde = ancienTuple.Item3;

                    if (solde - double.Parse(prix) < 0)
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"Votre solde de {solde}€ ne suffit pas");
                        return;
                    }
                    var chatid = config.CurrentChatId;

                    if (DataBase.SupprimerStockParId(int.Parse(msg[1])))
                    {
                        double nouveauSolde = ancienTuple.Item3 - double.Parse(prix);
                        config.UserSave[result] = Tuple.Create(ancienTuple.Item1, ancienTuple.Item2 + 1, nouveauSolde, ancienTuple.Item4);


                        Random rnd = new Random();
                        int nombre = rnd.Next(100, 999);

                        if(item.Brand == "accor")
                        {
                            await EnregistrerLogsVendu(item.Code, item.Value, prix, config.CurrentChatId, item.Brand);
                            await AvertirAchat(botClient, update, cancellationToken, prix, item.Brand);

                            await botClient.SendTextMessageAsync(config.CurrentChatId, $"Logs AccorHotel:\n📧 Email:<code>{item.Code}</code>\n🔑 Password:<code>{item.Pin}</code>\nPoints:{item.Value}",parseMode:ParseMode.Html);

                            return;
                        }

                        if(item.Brand == "carr")
                        {
                            codebarre.GenerateBarcode(item.Code, $"carr{chatid}_{nombre}.png");
                        }
                        else 
                        {
                            System.Diagnostics.Process.Start("generator.exe", $"{item.Code} {chatid}_{nombre}.png {item.Brand}");
                        }


                        await EnregistrerLogsVendu(item.Code,item.Value, prix, config.CurrentChatId,item.Brand);
                        await AvertirAchat(botClient, update, cancellationToken, prix,item.Brand);

                        if(item.Brand == "carr")
                        {
                            await EnvoyerQrCode(chatid, botClient, update, cancellationToken,item.Code,item.Pin);
                        }
                        else
                        {


                            await EnvoyerQrCode(chatid, botClient, update, cancellationToken);
                        }
                        return;
                    }
                }
                else
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Le produit n'est plus disponible");
                    return;
                }
            }
            catch
            {
                return;
            }
        }

        private static async Task<bool> EnvoyerQrCode(string clientId, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken, string carte = "",string pin = "")
        {
            try
            {
                while (true)
                {
                    var inlineKeyboard = new InlineKeyboardMarkup(new[]
           {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Home", "iHome")
                    }
                });
                    string dossierActuel = Directory.GetCurrentDirectory();
                    string[] fichiersPng = Directory.GetFiles(dossierActuel, "*.png");

                    foreach (string fichier in fichiersPng)
                    {
                        try
                        {
                            using (var stream = new FileStream(fichier, FileMode.Open, FileAccess.Read))
                            {
                                var photo = new InputOnlineFile(stream, Path.GetFileName(fichier));


                                if (photo.FileName.Contains("_"))
                                {
                                    var idphoto = photo.FileName.Split('_');

                                    if (idphoto[0].Contains("carr"))
                                    {
                                        idphoto[0] = idphoto[0].Remove(0, 4);

                                        await botClient.SendPhotoAsync(
                                    chatId: idphoto[0],
                                    caption: $"Merci de ton achat, Carte:{carte}:{pin}",
                                    photo: photo,
                                    parseMode: ParseMode.Html,
                                    replyMarkup: inlineKeyboard);
                                    }
                                    else
                                    {
                                        await botClient.SendPhotoAsync(
                                    chatId: idphoto[0],
                                    caption: $"Merci de ton achat",
                                    photo: photo,
                                    parseMode: ParseMode.Html,
                                    replyMarkup: inlineKeyboard);
                                    } 
                                }
                            }
                            System.IO.File.Delete(fichier);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("❌ Erreur en envoyant " + fichier + " : " + ex.Message);
                        }
                    }
                }
            }
            catch
            {

            }

            return false;
        }

        public static async Task AvertirAchat(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken,string prix,string brand)
        {
            try
            {
                foreach(var id in config.idAdmins)
                {
                    await botClient.SendTextMessageAsync(id, $"[+] Nouvelle commandes: Id:{config.CurrentChatId} | User:{config.CurrentPseudo} | Prix: {prix} | Categorie: {brand}");
                }
            }
            catch
            {

            }
        }
        public static async Task EnregistrerLogsVendu(string carte,string valeur,string prix,string id,string brand)
        {
            try
            {
                long.TryParse(config.CurrentChatId, out long userId);
                int.TryParse(valeur, out int val);
                double.TryParse(prix, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double prx);
                DataBase.EnregistrerTransaction(userId, brand, carte, "", val, prx);
            }
            catch
            {

            }
        }
    }
}
