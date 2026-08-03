using System;
using System.IO;
using ZXing;
using ZXing.Common;

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

            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Height = 120,
                    Width = 400,
                    Margin = 15
                }
            };

            var pixelData = writer.Write(codeToEncode);
            int width = pixelData.Width;
            int height = pixelData.Height;
            byte[] bgraPixels = pixelData.Pixels;

            using var fs = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);
            using var bw = new BinaryWriter(fs);

            int dataSize = bgraPixels.Length;
            int fileSize = 54 + dataSize;

            bw.Write((byte)'B');
            bw.Write((byte)'M');
            bw.Write(fileSize);
            bw.Write((short)0);
            bw.Write((short)0);
            bw.Write(54);

            bw.Write(40);
            bw.Write(width);
            bw.Write(-height);
            bw.Write((short)1);
            bw.Write((short)32);
            bw.Write(0);
            bw.Write(dataSize);
            bw.Write(2835);
            bw.Write(2835);
            bw.Write(0);
            bw.Write(0);

            bw.Write(bgraPixels);
            bw.Flush();
            fs.Flush();

            Console.WriteLine($"[GenerateBarcode Success] Image code-barres BMP créée : {outputFile} ({new FileInfo(outputFile).Length} octets)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GenerateBarcode Erreur] {ex.Message}");
        }
    }
}

