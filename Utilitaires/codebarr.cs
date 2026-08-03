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

            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Height = 130,
                    Width = 450,
                    Margin = 15,
                    PureBarcode = false
                }
            };

            var pixelData = writer.Write(cleanContent);
            int width = pixelData.Width;
            int height = pixelData.Height;
            byte[] rawPixels = pixelData.Pixels;

            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var bitmap = new SKBitmap(info);
            System.Runtime.InteropServices.Marshal.Copy(rawPixels, 0, bitmap.GetPixels(), rawPixels.Length);

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var fs = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);
            data.SaveTo(fs);
            fs.Flush();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GenerateBarcode Erreur] {ex.Message}");
        }
    }
}

