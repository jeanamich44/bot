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

            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var handle = GCHandle.Alloc(bgraPixels, GCHandleType.Pinned);
            try
            {
                using var bitmap = new SKBitmap();
                bitmap.InstallPixels(info, handle.AddrOfPinnedObject(), info.RowBytes);
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var fs = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
                data.SaveTo(fs);
            }
            finally
            {
                if (handle.IsAllocated) handle.Free();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GenerateBarcode Erreur] {ex.Message}");
        }
    }
}

