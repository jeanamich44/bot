using System.IO;
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
            string chatId = config.CurrentChatId;
            try
            {
                var msg = update.CallbackQuery.Data.Split('_');
                if (msg.Length < 2 || !int.TryParse(msg[1], out int stockId) || !long.TryParse(chatId, out long userId))
                {
                    await botClient.SendTextMessageAsync(chatId, "Utilisateur introuvable.", cancellationToken: cancellationToken);
                    return;
                }

                if (!DataBase.AcheterStockAtomique(userId, stockId, out var item, out double nouveauSolde, out _))
                {
                    var existant = DataBase.ObtenirStockParId(stockId);
                    if (existant == null)
                    {
                        await botClient.SendTextMessageAsync(chatId, "Le produit n'est plus disponible.", cancellationToken: cancellationToken);
                    }
                    else
                    {
                        var user = config.TrouverUtilisateur(userId);
                        double solde = user?.Solde ?? 0;
                        await botClient.SendTextMessageAsync(chatId, $"Votre solde de {solde}€ ne suffit pas.", cancellationToken: cancellationToken);
                    }
                    return;
                }

                Random rnd = new Random();
                int nombre = rnd.Next(100, 999);
                string fileName = $"carr{chatId}_{nombre}.png";

                try
                {
                    codebarre.GenerateBarcode(item!.Code, fileName);
                }
                catch (Exception bEx)
                {
                    Console.WriteLine($"[Barcode Error] {bEx.Message}");
                }

                string formattedPrice = nouveauSolde.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                double.TryParse((item.Price ?? "0").Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double prixDouble);
                formattedPrice = prixDouble.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

                bool okPhoto = await EnvoyerBarcodeDirect(fileName, chatId, botClient, update, cancellationToken, item.Code, item.Pin, item.Value);
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
                        chatId: chatId,
                        text: caption,
                        parseMode: ParseMode.Html,
                        replyMarkup: inlineKeyboard,
                        cancellationToken: cancellationToken);
                }

                _ = Task.Run(() => AvertirAchat(botClient, cancellationToken, formattedPrice, item.Brand, chatId));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DemandeAchat Erreur] {ex.Message}\n{ex.StackTrace}");
                try
                {
                    await botClient.SendTextMessageAsync(chatId, "❌ Une erreur est survenue lors du traitement de l'achat. Veuillez réessayer.", cancellationToken: cancellationToken);
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

        public static async Task AvertirAchat(ITelegramBotClient botClient, CancellationToken cancellationToken, string montant, string brand, string userChatId)
        {
            foreach (var id in config.idAdmins)
            {
                try
                {
                    await botClient.SendTextMessageAsync(id, $"🛒 *ACHAT EFFECTUÉ*\n\nUser: `{userChatId}`\nMarque: *{brand}*\nPrix: {montant}€", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                }
                catch { }
            }
        }
    }
}
