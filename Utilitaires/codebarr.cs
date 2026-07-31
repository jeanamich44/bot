using SkiaSharp;
using ZXing;
using ZXing.Common;
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
            using var bitmap = new SKBitmap(pixelData.Width, pixelData.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var pixelsHandle = System.Runtime.InteropServices.GCHandle.Alloc(pixelData.Pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                bitmap.InstallPixels(bitmap.Info, pixelsHandle.AddrOfPinnedObject(), bitmap.RowBytes);
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = System.IO.File.Create(outputFile);
                data.SaveTo(stream);
            }
            finally
            {
                pixelsHandle.Free();
            }
        }
        catch
        {

        }
    }

    const int CARD_W = 1011;
    const int CARD_H = 638;



    private static void DrawBarcodeSkia(SKCanvas canvas, string content, int cardW, int cardH)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.CODE_128,
            Options = new EncodingOptions
            {
                Height = (int)(cardH * 0.19f),
                Width = (int)(cardW * 0.85f),
                Margin = 0,
                PureBarcode = true
            }
        };

        var pd = writer.Write(content);
        using var barcodeBmp = new SKBitmap(pd.Width, pd.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(pd.Pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            barcodeBmp.InstallPixels(barcodeBmp.Info, handle.AddrOfPinnedObject(), barcodeBmp.RowBytes);
            int targetW = (int)(cardW * 0.82f);
            int targetH = (int)(cardH * 0.16f);
            int x = (cardW - targetW) / 2;
            int y = (int)(cardH * 0.78f);
            canvas.DrawBitmap(barcodeBmp, new SKRect(x, y, x + targetW, y + targetH));
        }
        finally
        {
            handle.Free();
        }
    }

    static string NormalizeDigits(string input)
    {
        var arr = new char[input.Length];
        int j = 0;
        foreach (var c in input)
            if (char.IsDigit(c)) arr[j++] = c;
        return new string(arr, 0, j);
    }

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

    public static void GenerateMcDoCode128Card(string cardNumber, string outputFile, bool addPrefixM = false)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
            throw new ArgumentException("Numéro vide");

        string payload = NormalizeAlnum(cardNumber);
        if (addPrefixM) payload = "M" + payload;

        using var bitmap = new SKBitmap(CARD_W, CARD_H);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

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

        using var barcodeBmp = new SKBitmap(pd.Width, pd.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(pd.Pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            barcodeBmp.InstallPixels(barcodeBmp.Info, handle.AddrOfPinnedObject(), barcodeBmp.RowBytes);
            int targetW = (int)(CARD_W * 0.9f);
            int targetH = (int)(CARD_H * 0.32f);
            int x = (CARD_W - targetW) / 2;
            int y = (int)(CARD_H * 0.28f);
            canvas.DrawBitmap(barcodeBmp, new SKRect(x, y, x + targetW, y + targetH));
        }
        finally
        {
            handle.Free();
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = System.IO.File.Create(outputFile);
        data.SaveTo(stream);
    }

    public static void GenerateMcDoQrCard(string content, string outputFile, int qrSizePx = 600)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Contenu vide");

        string payload = NormalizeAlnum(content);

        using var bitmap = new SKBitmap(CARD_W, CARD_H);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

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

        using var qrBmp = new SKBitmap(pd.Width, pd.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(pd.Pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            qrBmp.InstallPixels(qrBmp.Info, handle.AddrOfPinnedObject(), qrBmp.RowBytes);
            int size = (int)(CARD_H * 0.6f);
            int x = (CARD_W - size) / 2;
            int y = (CARD_H - size) / 2;
            canvas.DrawBitmap(qrBmp, new SKRect(x, y, x + size, y + size));
        }
        finally
        {
            handle.Free();
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = System.IO.File.Create(outputFile);
        data.SaveTo(stream);
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



    static bool IsAllDigits(string s)
    {
        foreach (char c in s)
            if (!char.IsDigit(c)) return false;
        return true;
    }

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
