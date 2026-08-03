using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace ChezRheyyBot
{
    internal class achat
    {
        public static async Task DemandeAchat(string messageSystem, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var msg = update.CallbackQuery.Data.Split('_');
                var item = DataBase.ObtenirStockParId(int.Parse(msg[1]));
                int result = config.UserSave.FindIndex(tuple => tuple.Item1 == long.Parse(config.CurrentChatId));

                if (result == -1)
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Utilisateur introuvable.", cancellationToken: cancellationToken);
                    return;
                }

                var ancienTuple = config.UserSave[result];

                if (item != null)
                {
                    double prixDouble = 0.0;
                    string rawPrice = (item.Price ?? "0").Replace(',', '.');
                    if (!double.TryParse(rawPrice, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out prixDouble))
                    {
                        double.TryParse(item.Price, out prixDouble);
                    }

                    var solde = ancienTuple.Item3;

                    if (solde - prixDouble < 0)
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"Votre solde de {solde}€ ne suffit pas.", cancellationToken: cancellationToken);
                        return;
                    }

                    var chatid = config.CurrentChatId;

                    if (DataBase.SupprimerStockParId(int.Parse(msg[1])))
                    {
                        double nouveauSolde = ancienTuple.Item3 - prixDouble;
                        config.UserSave[result] = Tuple.Create(ancienTuple.Item1, ancienTuple.Item2 + 1, nouveauSolde, ancienTuple.Item4);
                        DataBase.SauvegarderUtilisateurIndividuel(ancienTuple.Item1);

                        Random rnd = new Random();
                        int nombre = rnd.Next(100, 999);
                        string fileName = $"carr{chatid}_{nombre}.png";

                        try
                        {
                            codebarre.GenerateBarcode(item.Code, fileName);
                        }
                        catch (Exception bEx)
                        {
                            Console.WriteLine($"[Barcode Error] {bEx.Message}");
                        }

                        string formattedPrice = prixDouble.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                        await EnregistrerLogsVendu(item.Code, item.Pin, item.Value, formattedPrice, config.CurrentChatId, item.Brand);

                        bool okPhoto = await EnvoyerBarcodeDirect(fileName, chatid, botClient, update, cancellationToken, item.Code, item.Pin, item.Value);
                        if (!okPhoto)
                        {
                            var inlineKeyboard = new InlineKeyboardMarkup(new[]
                            {
                                new[]
                                {
                                    InlineKeyboardButton.WithCallbackData("🏠 Accueil", "iHome")
                                }
                            });
                            string safeVal = System.Net.WebUtility.HtmlEncode(item.Value ?? "");
                            string safeCarte = System.Net.WebUtility.HtmlEncode(item.Code ?? "");
                            string safePin = System.Net.WebUtility.HtmlEncode(item.Pin ?? "");

                            string valText = string.IsNullOrWhiteSpace(safeVal) ? "" : $"💰 Solde : <b>{safeVal}€</b>\n";
                            string pinText = string.IsNullOrWhiteSpace(safePin) ? "" : $"\n🔑 PIN : <code>{safePin}</code>";
                            string caption = $"✅ <b>Merci pour votre achat !</b>\n\n{valText}💳 Carte Carrefour : <code>{safeCarte}</code>{pinText}";

                            await botClient.SendTextMessageAsync(
                                chatId: chatid,
                                text: caption,
                                parseMode: ParseMode.Html,
                                replyMarkup: inlineKeyboard,
                                cancellationToken: cancellationToken);
                        }

                        _ = Task.Run(() => AvertirAchat(botClient, update, cancellationToken, formattedPrice, item.Brand));
                        return;
                    }
                }
                else
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Le produit n'est plus disponible.", cancellationToken: cancellationToken);
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DemandeAchat Erreur] {ex.Message}\n{ex.StackTrace}");
                try
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "❌ Une erreur est survenue lors du traitement de l'achat. Veuillez réessayer.", cancellationToken: cancellationToken);
                }
                catch { }
            }
        }

        private static async Task<bool> EnvoyerBarcodeDirect(string fileName, string clientId, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken, string carte = "", string pin = "", string val = "")
        {
            try
            {
                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("🏠 Accueil", "iHome")
                    }
                });

                var fileInfo = new FileInfo(fileName);
                if (fileInfo.Exists && fileInfo.Length > 0)
                {
                    using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var photo = new InputOnlineFile(stream, Path.GetFileName(fileName));
                        string safeVal = System.Net.WebUtility.HtmlEncode(val ?? "");
                        string safeCarte = System.Net.WebUtility.HtmlEncode(carte ?? "");
                        string safePin = System.Net.WebUtility.HtmlEncode(pin ?? "");

                        string valText = string.IsNullOrWhiteSpace(safeVal) ? "" : $"💰 Solde : <b>{safeVal}€</b>\n";
                        string pinText = string.IsNullOrWhiteSpace(safePin) ? "" : $"\n🔑 PIN : <code>{safePin}</code>";
                        string caption = $"✅ <b>Merci pour votre achat !</b>\n\n{valText}💳 Carte Carrefour : <code>{safeCarte}</code>{pinText}";

                        await botClient.SendPhotoAsync(
                            chatId: clientId,
                            photo: photo,
                            caption: caption,
                            parseMode: ParseMode.Html,
                            replyMarkup: inlineKeyboard,
                            cancellationToken: cancellationToken);
                    }

                    try
                    {
                        System.IO.File.Delete(fileName);
                    }
                    catch { }

                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EnvoyerBarcodeDirect Erreur] {ex.Message}");
            }

            return false;
        }

        public static async Task AvertirAchat(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken, string montant, string brand)
        {
            foreach (var id in config.idAdmins)
            {
                try
                {
                    await botClient.SendTextMessageAsync(id, $"🛒 *ACHAT EFFECTUÉ*\n\nUser: `{config.CurrentChatId}`\nMarque: *{brand}*\nPrix: {montant}€", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                }
                catch { }
            }
        }

        public static async Task EnregistrerLogsVendu(string carte, string pin, string solde, string prix, string userId, string brand)
        {
            try
            {
                int.TryParse(solde, out int val);
                double.TryParse(prix, out double p);
                long.TryParse(userId, out long uId);
                DataBase.EnregistrerTransaction(uId, brand, carte, pin ?? "", val, p);
            }
            catch { }
        }
    }
}
