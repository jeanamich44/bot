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
                    var prix = item.Price;
                    var solde = ancienTuple.Item3;

                    if (solde - double.Parse(prix) < 0)
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"Votre solde de {solde}€ ne suffit pas.", cancellationToken: cancellationToken);
                        return;
                    }

                    var chatid = config.CurrentChatId;

                    if (DataBase.SupprimerStockParId(int.Parse(msg[1])))
                    {
                        double nouveauSolde = ancienTuple.Item3 - double.Parse(prix);
                        config.UserSave[result] = Tuple.Create(ancienTuple.Item1, ancienTuple.Item2 + 1, nouveauSolde, ancienTuple.Item4);
                        DataBase.SauvegarderUtilisateurIndividuel(ancienTuple.Item1);

                        Random rnd = new Random();
                        int nombre = rnd.Next(100, 999);
                        string fileName = $"carr{chatid}_{nombre}.png";

                        codebarre.GenerateBarcode(item.Code, fileName);

                        await EnregistrerLogsVendu(item.Code, item.Pin, item.Value, prix, config.CurrentChatId, item.Brand);
                        await AvertirAchat(botClient, update, cancellationToken, prix, item.Brand);
                        await EnvoyerBarcodeDirect(fileName, chatid, botClient, update, cancellationToken, item.Code, item.Pin);
                        return;
                    }
                }
                else
                {
                    await botClient.SendTextMessageAsync(config.CurrentChatId, "Le produit n'est plus disponible.", cancellationToken: cancellationToken);
                    return;
                }
            }
            catch { }
        }

        private static async Task<bool> EnvoyerBarcodeDirect(string fileName, string clientId, ITelegramBotClient botClient, Update update, CancellationToken cancellationToken, string carte = "", string pin = "")
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

                if (System.IO.File.Exists(fileName))
                {
                    using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                    {
                        var photo = new InputOnlineFile(stream, Path.GetFileName(fileName));
                        string pinText = string.IsNullOrWhiteSpace(pin) ? "" : $"\n🔑 PIN : <code>{pin}</code>";
                        string caption = $"✅ <b>Merci pour votre achat !</b>\n\n💳 Carte Carrefour : <code>{carte}</code>{pinText}";

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
            catch { }

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
