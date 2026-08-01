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

            var pixelData = writer.Write(content);
            int width = pixelData.Width;
            int height = pixelData.Height;
            byte[] bgraPixels = pixelData.Pixels;

            using var fs = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            int dataSize = bgraPixels.Length;
            int fileSize = 54 + dataSize;

            // Header BMP File (14 octets)
            bw.Write((byte)'B');
            bw.Write((byte)'M');
            bw.Write(fileSize);
            bw.Write((short)0);
            bw.Write((short)0);
            bw.Write(54);

            // Header BMP Info (40 octets)
            bw.Write(40);
            bw.Write(width);
            bw.Write(-height); // Hauteur négative pour affichage de haut en bas
            bw.Write((short)1);
            bw.Write((short)32);
            bw.Write(0);
            bw.Write(dataSize);
            bw.Write(2835);
            bw.Write(2835);
            bw.Write(0);
            bw.Write(0);

            // Écriture directe des pixels (32-bit BGRA)
            bw.Write(bgraPixels);
            bw.Flush();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GenerateBarcode Erreur] {ex.Message}");
        }
    }
}
