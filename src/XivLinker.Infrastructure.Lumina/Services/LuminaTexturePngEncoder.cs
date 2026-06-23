using Lumina.Data.Files;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XivLinker.Infrastructure.Lumina.Services;

internal static class LuminaTexturePngEncoder
{
    public static byte[]? Encode(TexFile texture)
    {
        byte[] imageData = texture.ImageData;
        if (imageData.Length == 0)
        {
            return null;
        }

        int width = texture.Header.Width;
        int height = texture.Header.Height;
        int stride = width * 4;

        BitmapSource bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            imageData,
            stride);
        bitmap.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
