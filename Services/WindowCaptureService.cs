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
}
