using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
namespace DoneBubble.Services;
public sealed class WindowCaptureService
{
    public byte[]? CaptureScreen()
    {
        var rect = System.Windows.Forms.Screen.AllScreens
            .Select(screen => screen.Bounds)
            .Aggregate(System.Drawing.Rectangle.Union);
        int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0 || width > 8000 || height > 8000) return null;
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
        using var stream = new MemoryStream();
        var encoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(item => item.FormatID == ImageFormat.Jpeg.Guid);
        if (encoder == null) { bitmap.Save(stream, ImageFormat.Jpeg); return stream.ToArray(); }
        using var quality = new EncoderParameters(1);
        quality.Param[0] = new EncoderParameter(Encoder.Quality, 68L);
        bitmap.Save(stream, encoder, quality);
        return stream.ToArray();
    }
    public byte[] OptimizeForModel(byte[] image, int maxDimension = 1280)
        => Reencode(image, maxDimension, 58L);

    // Evidence is kept locally for later inspection, but should not embed a
    // full multi-monitor bitmap for every timer tick in the JSON log.
    public byte[] OptimizeForArchive(byte[] image, int maxDimension = 1600)
        => Reencode(image, maxDimension, 50L);

    private static byte[] Reencode(byte[] image, int maxDimension, long jpegQuality)
    {
        using var source = new Bitmap(new MemoryStream(image));
        double scale = Math.Min(1d, maxDimension / (double)Math.Max(source.Width, source.Height));
        int width = Math.Max(1, (int)Math.Round(source.Width * scale));
        int height = Math.Max(1, (int)Math.Round(source.Height * scale));
        using var resized = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(resized))
        {
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(source, 0, 0, width, height);
        }
        using var output = new MemoryStream();
        var encoder = ImageCodecInfo.GetImageEncoders().First(item => item.FormatID == ImageFormat.Jpeg.Guid);
        using var quality = new EncoderParameters(1);
        quality.Param[0] = new EncoderParameter(Encoder.Quality, jpegQuality);
        resized.Save(output, encoder, quality);
        return output.ToArray();
    }
}
