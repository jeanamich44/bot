namespace ChezRheyyBot
{
    internal class Utilisateur
    {
        public long Id { get; set; }
        public int Achat { get; set; }
        public double Solde { get; set; }
        public bool IsBanned { get; set; }
    }

    internal class BotContexte
    {
        public string ChatId { get; set; } = "";
        public string Pseudo { get; set; } = "";
        public string MsgId { get; set; } = "";
    }
}
