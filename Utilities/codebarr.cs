using ZXing;
using ZXing.Common;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using ZXing.QrCode;
using System.Text;

class codebarre
{
    public static void GenerateBarcode(string content, string outputFile)
    {
        try
        {
            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Height = 100,
                    Width = 300,
                    Margin = 10
                }
            };

            var pixelData = writer.Write(content);

            // Crée un Bitmap à partir des données brutes
            using (var bitmap = new Bitmap(pixelData.Width, pixelData.Height, PixelFormat.Format32bppRgb))
            {
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, pixelData.Width, pixelData.Height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppRgb);

                try
                {
                    // Copie les pixels dans le bitmap
                    Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }

                bitmap.Save(outputFile, ImageFormat.Png);
            }
        }
        catch
        {

        }
    }
    const int CARD_W = 1011;
    const int CARD_H = 638;
    const int DPI = 300;

    public static void GenerateFnacCard(string loyaltyNumber16, string outputFile)
    {
        if (string.IsNullOrWhiteSpace(loyaltyNumber16))
            throw new ArgumentException("Numéro vide");
        if (NormalizeDigits(loyaltyNumber16).Length != 16)
            throw new ArgumentException("Le numéro doit contenir 16 chiffres");

        string digits16 = NormalizeDigits(loyaltyNumber16);

        using (var bmp = new Bitmap(CARD_W, CARD_H, PixelFormat.Format32bppArgb))
        {
            bmp.SetResolution(DPI, DPI);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Black);

                // fond esthétique
                var fnacYellow = Color.FromArgb(0xFF, 0xD2, 0x00);
                DrawBackground(g, fnacYellow);

                // logo simple "fnac"
                DrawLogo(g);

                // textes (numéro groupé)
              //  DrawTexts(g, digits16);

                // code-barres
                DrawBarcode(g, digits16);
            }

            bmp.Save(outputFile, ImageFormat.Png);
        }
    }

    // --- FONCTIONS INTERNES ---

    static void DrawBackground(Graphics g, Color fnacYellow)
    {
        using var path = RoundedRect(new Rectangle(0, 0, CARD_W, CARD_H), 28);
        using var clip = new Region(path);
        g.Clip = clip;

        // Bande diagonale jaune
        using (var yellow = new SolidBrush(fnacYellow))
        {
            PointF[] poly = new[]
            {
                new PointF(0, CARD_H * 0.15f),
                new PointF(CARD_W * 0.70f, 0),
                new PointF(CARD_W, 0),
                new PointF(CARD_W, CARD_H * 0.35f),
                new PointF(CARD_W * 0.30f, CARD_H * 0.50f),
                new PointF(0, CARD_H * 0.50f),
            };
            g.FillPolygon(yellow, poly);
        }

        g.ResetClip();
    }

    static void DrawLogo(Graphics g)
    {
        var rect = new Rectangle((int)(CARD_W * 0.06f), (int)(CARD_H * 0.08f), 200, 100);
        using var f = new Font("Segoe UI", 60, FontStyle.Bold, GraphicsUnit.Pixel);
        using var w = new SolidBrush(Color.White);
        g.DrawString("fnac", f, w, rect);
    }

    static void DrawTexts(Graphics g, string digits16)
    {
        // Numéro groupé 4x4
        string grouped = $"{digits16.Substring(0, 4)} {digits16.Substring(4, 4)} {digits16.Substring(8, 4)} {digits16.Substring(12, 4)}";

        using (var numFont = new Font("Consolas", 42, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var numBrush = new SolidBrush(Color.White))
        {
            g.DrawString(grouped, numFont, numBrush, new PointF(CARD_W * 0.06f, CARD_H * 0.70f));
        }
    }

    static void DrawBarcode(Graphics g, string digits16)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.CODE_128,
            Options = new EncodingOptions
            {
                Height = (int)(CARD_H * 0.19f),
                Width = (int)(CARD_W * 0.85f),
                Margin = 0,
                PureBarcode = true
            }
        };

        var pixelData = writer.Write(digits16);

        using var barcodeBmp = new Bitmap(pixelData.Width, pixelData.Height, PixelFormat.Format32bppArgb);
        var bd = barcodeBmp.LockBits(new Rectangle(0, 0, pixelData.Width, pixelData.Height),
                                     ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try { Marshal.Copy(pixelData.Pixels, 0, bd.Scan0, pixelData.Pixels.Length); }
        finally { barcodeBmp.UnlockBits(bd); }

        int targetW = (int)(CARD_W * 0.82f);
        int targetH = (int)(CARD_H * 0.16f);
        int x = (CARD_W - targetW) / 2;
        int y = (int)(CARD_H * 0.78f);
        g.DrawImage(barcodeBmp, new Rectangle(x, y, targetW, targetH));
    }

    static string NormalizeDigits(string input)
    {
        var arr = new char[input.Length];
        int j = 0;
        foreach (var c in input)
            if (char.IsDigit(c)) arr[j++] = c;
        return new string(arr, 0, j);
    }

    static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // =========================
    // --- CORRECTION McDo ---
    // =========================

    // Nouveau : conserve lettres+chiffres en MAJ (ex: "VOHB0SJ0" -> "VOHB0SJ0")
    static string NormalizeAlnum(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Génère une carte McDo avec Code128 (ne supprime plus les lettres).
    /// </summary>
    public static void GenerateMcDoCode128Card(string cardNumber, string outputFile, bool addPrefixM = false)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
            throw new ArgumentException("Numéro vide");

        // IMPORTANT : on garde lettres+chiffres, pas seulement les chiffres
        string payload = NormalizeAlnum(cardNumber);
        if (addPrefixM) payload = "M" + payload;

        using (var bmp = new Bitmap(CARD_W, CARD_H, PixelFormat.Format32bppArgb))
        {
            bmp.SetResolution(DPI, DPI);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Titre
                using (var titleF = new Font("Arial", 36, FontStyle.Bold, GraphicsUnit.Pixel))
                    g.DrawString("McDonald's - Carte Fidélité", titleF, Brushes.Black, CARD_W * 0.05f, CARD_H * 0.06f);

                // Génère et place le code 128 (avec le payload complet)
                var writer = new BarcodeWriterPixelData
                {
                    Format = BarcodeFormat.CODE_128,
                    Options = new EncodingOptions
                    {
                        Height = (int)(CARD_H * 0.28f),
                        Width = (int)(CARD_W * 0.9f),
                        Margin = 0,
                        PureBarcode = true
                    }
                };
                var pd = writer.Write(payload);

                using var barcodeBmp = new Bitmap(pd.Width, pd.Height, PixelFormat.Format32bppArgb);
                var bd = barcodeBmp.LockBits(new Rectangle(0, 0, pd.Width, pd.Height),
                                             ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try { Marshal.Copy(pd.Pixels, 0, bd.Scan0, pd.Pixels.Length); }
                finally { barcodeBmp.UnlockBits(bd); }

                int targetW = (int)(CARD_W * 0.9f);
                int targetH = (int)(CARD_H * 0.32f);
                int x = (CARD_W - targetW) / 2;
                int y = (int)(CARD_H * 0.28f);
                g.DrawImage(barcodeBmp, new Rectangle(x, y, targetW, targetH));

                // Affiche le code complet en dessous (MAJ + préfixe si demandé)
                using (var numFont = new Font("Consolas", 28, FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    string display = payload; // déjà normalisé en alphanumérique
                    var sz = g.MeasureString(display, numFont);
                    float tx = (CARD_W - sz.Width) / 2;
                    float ty = y + targetH + 8;
                    g.DrawString(display, numFont, Brushes.Black, tx, ty);
                }
            }

            bmp.Save(outputFile, ImageFormat.Png);
        }
    }

    /// <summary>
    /// Génère une carte McDo avec QR (utilise aussi NormalizeAlnum si tu fournis un simple code).
    /// </summary>
    public static void GenerateMcDoQrCard(string content, string outputFile, int qrSizePx = 600)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Contenu vide");

        string payload = NormalizeAlnum(content); // si tu fournis une URL complète, tu peux l'envoyer telle quelle

        using (var bmp = new Bitmap(CARD_W, CARD_H, PixelFormat.Format32bppArgb))
        {
            bmp.SetResolution(DPI, DPI);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (var titleF = new Font("Arial", 28, FontStyle.Bold, GraphicsUnit.Pixel))
                    g.DrawString("McDonald's - Carte Fidélité (QR)", titleF, Brushes.Black, CARD_W * 0.05f, CARD_H * 0.06f);

                var qrWriter = new ZXing.BarcodeWriterPixelData
                {
                    Format = BarcodeFormat.QR_CODE,
                    Options = new QrCodeEncodingOptions
                    {
                        Height = qrSizePx,
                        Width = qrSizePx,
                        Margin = 1,
                        ErrorCorrection = ZXing.QrCode.Internal.ErrorCorrectionLevel.M
                    }
                };

                var pd = qrWriter.Write(payload);

                using var qrBmp = new Bitmap(pd.Width, pd.Height, PixelFormat.Format32bppArgb);
                var bd = qrBmp.LockBits(new Rectangle(0, 0, pd.Width, pd.Height),
                                        ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try { Marshal.Copy(pd.Pixels, 0, bd.Scan0, pd.Pixels.Length); }
                finally { qrBmp.UnlockBits(bd); }

                int size = (int)(CARD_H * 0.6f);
                int x = (CARD_W - size) / 2;
                int y = (CARD_H - size) / 2;
                g.DrawImage(qrBmp, new Rectangle(x, y, size, size));

                // texte sous QR
                using (var f = new Font("Consolas", 20, FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    string disp = payload;
                    var sz = g.MeasureString(disp, f);
                    g.DrawString(disp, f, Brushes.Black, (CARD_W - sz.Width) / 2, y + size + 8);
                }
            }

            bmp.Save(outputFile, ImageFormat.Png);
        }
    }

    public static void GenerateMcDoCardForKiosk(string cardNumber, string outputFile, bool preferQr = false, bool addPrefixM = false)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
            throw new ArgumentException("Numéro vide");

        string alnum = NormalizeAlnum(cardNumber);
        if (alnum.Length == 0)
            throw new ArgumentException("Aucun caractère alphanumérique dans le numéro");

        if (preferQr)
            GenerateMcDoQrCard(addPrefixM ? ("M" + alnum) : alnum, outputFile);
        else
            GenerateMcDoCode128Card(alnum, outputFile, addPrefixM);
    }


    // les fonction pour monoprix

    // ===============================
    //  FONCTIONS POUR MONOPRIX
    // ===============================

    public static void GenerateMonoprixBarcode(string raw, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("Numéro vide");

        string content = raw;
        BarcodeFormat format;

        // Si 12 chiffres => on calcule la clé et on génère un EAN-13
        if (IsAllDigits(raw) && raw.Length == 12)
        {
            int check = ComputeEan13CheckDigit(raw);
            content = raw + check;
            format = BarcodeFormat.EAN_13;
            Console.WriteLine($"Génération EAN-13 : {content}");
        }
        else
        {
            // Sinon Code 128 (Monoprix les accepte aussi)
            format = BarcodeFormat.CODE_128;
            Console.WriteLine($"Génération Code128 : {content}");
        }

        var writer = new ZXing.BarcodeWriterPixelData
        {
            Format = format,
            Options = new EncodingOptions
            {
                Height = 120,
                Width = Math.Max(300, content.Length * 20),
                Margin = 2,
                PureBarcode = true
            }
        };

        // Génère les pixels
        var pixelData = writer.Write(content);

        // Création du bitmap
        using (Bitmap bmp = new Bitmap(pixelData.Width, pixelData.Height, PixelFormat.Format32bppArgb))
        {
            var data = bmp.LockBits(new Rectangle(0, 0, pixelData.Width, pixelData.Height),
                                    ImageLockMode.WriteOnly,
                                    PixelFormat.Format32bppArgb);

            try
            {
                Marshal.Copy(pixelData.Pixels, 0, data.Scan0, pixelData.Pixels.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            // Sauvegarde
            bmp.Save(outputPath, ImageFormat.Png);
        }
    }

    // Vérifie si tous les caractères sont des chiffres
    static bool IsAllDigits(string s)
    {
        foreach (char c in s)
            if (!char.IsDigit(c)) return false;
        return true;
    }

    // Calcul de clé EAN-13
    static int ComputeEan13CheckDigit(string twelveDigits)
    {
        int sum = 0;
        for (int i = 0; i < 12; i++)
        {
            int digit = twelveDigits[11 - i] - '0';
            sum += (i % 2 == 0) ? digit : 3 * digit;
        }
        return (10 - (sum % 10)) % 10;
    }


}

