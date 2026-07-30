using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ChezRheyyBot
{
    internal class cinema
    {
        public static async Task AcheterCinema(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if(update.CallbackQuery.Data == "iPatheGaumont")
                {
                    //faire les pathe

                    int result = config.UserSave.FindIndex(tuple => tuple.Item1 == long.Parse(config.CurrentChatId));

                    if (result == -1)
                    {
                        return;
                    }

                    var ancienTuple = config.UserSave[result];

                    var item = DataBase.ObtenirStocksParBrand("Pathe");

                    if (item.Count == 0)
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"😞 Aucun stock 'Cinema', veuillez revenir plus tard");
                        return;
                    }

                    if (item != null)
                    {
                        var items = DataBase.ObtenirStockParId(item.First().Id);

                        if (item != null)
                        {
                            var prix = 55;
                            var solde = ancienTuple.Item3;

                            if (solde - prix < 0)
                            {
                                await botClient.SendTextMessageAsync(config.CurrentChatId, $"Votre solde de {solde}€ ne suffit pas");
                                return;
                            }
                            var chatid = config.CurrentChatId;

                            if (DataBase.SupprimerStockParId(item.First().Id))
                            {
                                double nouveauSolde = ancienTuple.Item3 - prix;
                                config.UserSave[result] = Tuple.Create(ancienTuple.Item1, ancienTuple.Item2 + 1, nouveauSolde, ancienTuple.Item4);


                                Random rnd = new Random();
                                int nombre = rnd.Next(100, 999);

                                await achat.EnregistrerLogsVendu(items.Code, items.Value, prix.ToString(), config.CurrentChatId, items.Brand);
                                await achat.AvertirAchat(botClient, update, cancellationToken, prix.ToString(), items.Brand);

                                await botClient.SendTextMessageAsync(config.CurrentChatId, $"Voici Votre achat Cinema:  \n\nCode CinePass: {items.Code}\nTuto: A venir...\nEn cas de soucis @RheyyFonda");

                                return;
                            }
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(config.CurrentChatId, "Le produit n'est plus disponible");
                            return;
                        }
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, "Le produit n'est plus disponible");
                        return;
                    }


                }
                else
                {
                    int result = config.UserSave.FindIndex(tuple => tuple.Item1 == long.Parse(config.CurrentChatId));

                    if (result == -1)
                    {
                        return;
                    }

                    var ancienTuple = config.UserSave[result];

                    var item = DataBase.ObtenirStocksParBrand("Ugc");

                    if (item.Count == 0)
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, $"😞 Aucun stock 'Cinema', veuillez revenir plus tard");
                        return;
                    }

                    if (item != null)
                    {
                        var items = DataBase.ObtenirStockParId(item.First().Id);

                        if (item != null)
                        {
                            var prix = 55;
                            var solde = ancienTuple.Item3;

                            if (solde - prix < 0)
                            {
                                await botClient.SendTextMessageAsync(config.CurrentChatId, $"Votre solde de {solde}€ ne suffit pas");
                                return;
                            }
                            var chatid = config.CurrentChatId;

                            if (DataBase.SupprimerStockParId(item.First().Id))
                            {
                                double nouveauSolde = ancienTuple.Item3 - prix;
                                config.UserSave[result] = Tuple.Create(ancienTuple.Item1, ancienTuple.Item2 + 1, nouveauSolde, ancienTuple.Item4);


                                Random rnd = new Random();
                                int nombre = rnd.Next(100, 999);

                                await achat.EnregistrerLogsVendu(items.Code, items.Value, prix.ToString(), config.CurrentChatId, items.Brand);
                                await achat.AvertirAchat(botClient, update, cancellationToken, prix.ToString(), items.Brand);

                                await botClient.SendTextMessageAsync(config.CurrentChatId, $"Voici Votre achat Cinema:  \n\nCode CinePass: {items.Code}\nTuto: liens\nEn cas de soucis @RheyyFonda");

                                return;
                            }
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(config.CurrentChatId, "Le produit n'est plus disponible");
                            return;
                        }
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(config.CurrentChatId, "Le produit n'est plus disponible");
                        return;
                    }
                }
            }
            catch
            {

            }
        }
    }
}
