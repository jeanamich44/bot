using System;
using System.IO;
using System.Runtime.InteropServices;
using ZXing;
using ZXing.Common;
using SkiaSharp;

class codebarre
{
    public static void GenerateBarcode(string content, string outputFile)
    {
        try
        {
            string cleanContent = System.Text.RegularExpressions.Regex.Replace(content ?? "", @"[^\x20-\x7E]", "").Trim();
            if (string.IsNullOrWhiteSpace(cleanContent))
            {
                Console.WriteLine("[GenerateBarcode Warning] Code vide ou invalide pour génération.");
                return;
            }

            string digitsOnly = System.Text.RegularExpressions.Regex.Replace(cleanContent, @"\s+", "");
            string codeToEncode = string.IsNullOrWhiteSpace(digitsOnly) ? cleanContent : digitsOnly;

            ZXing.Common.BitMatrix? matrix = null;
            try
            {
                var writer = new ZXing.OneD.Code128Writer();
                matrix = writer.encode(codeToEncode, BarcodeFormat.CODE_128, 480, 140, null);
            }
            catch
            {
                try
                {
                    var multiWriter = new ZXing.MultiFormatWriter();
                    matrix = multiWriter.encode(codeToEncode, BarcodeFormat.CODE_128, 480, 140, null);
                }
                catch (Exception exInner)
                {
                    Console.WriteLine($"[GenerateBarcode MultiWriter Error] {exInner.Message}");
                }
            }

            if (matrix == null)
            {
                Console.WriteLine("[GenerateBarcode Error] Impossible de générer la matrice de code-barres.");
                return;
            }

            int width = matrix.Width;
            int height = matrix.Height;

            using var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);

            using var blackPaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill, IsAntialias = false };
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (matrix[x, y])
                    {
                        canvas.DrawRect(x, y, 1, 1, blackPaint);
                    }
                }
            }
            canvas.Flush();

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var fs = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);
            data.SaveTo(fs);
            fs.Flush();
            Console.WriteLine($"[GenerateBarcode Success] Image code-barres créée : {outputFile} ({new FileInfo(outputFile).Length} octets)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GenerateBarcode Erreur] {ex.Message}");
        }
    }
}

