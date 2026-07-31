using SkiaSharp;
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
}
