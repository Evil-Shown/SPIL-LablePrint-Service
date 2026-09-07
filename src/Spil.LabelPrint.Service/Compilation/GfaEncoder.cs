using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;

namespace Spil.LabelPrint.Service.Compilation;

internal static class GfaEncoder
{
    public static string FromBitmap(Bitmap bmp, int threshold = 168)
    {
        var w = bmp.Width;
        var h = bmp.Height;
        var bytesPerRow = (w + 7) / 8;
        var total = bytesPerRow * h;
        var bytes = new byte[total];

        var data = bmp.LockBits(
            new Rectangle(0, 0, w, h),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            var stride = data.Stride;
            var buf = new byte[Math.Abs(stride) * h];
            Marshal.Copy(data.Scan0, buf, 0, buf.Length);
            for (var y = 0; y < h; y++)
            {
                var row = y * stride;
                for (var x = 0; x < w; x++)
                {
                    var i = row + x * 4;
                    var b = buf[i];
                    var g = buf[i + 1];
                    var r = buf[i + 2];
                    var a = buf[i + 3];
                    var lum = 0.299 * r + 0.587 * g + 0.114 * b;
                    if (a > 20 && lum < threshold)
                        bytes[y * bytesPerRow + (x >> 3)] |= (byte)(0x80 >> (x & 7));
                }
            }
        }
        finally
        {
            bmp.UnlockBits(data);
        }

        var hex = new StringBuilder(bytes.Length * 2);
        foreach (var by in bytes)
            hex.Append(by.ToString("X2"));
        return $"^GFA,{total},{total},{bytesPerRow},{hex}";
    }

    public static string TextField(
        string text,
        int width,
        int height,
        float fontPx,
        string fontFamily,
        FontStyle style,
        StringAlignment align,
        bool inverted)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        fontPx = Math.Max(6, fontPx);
        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(inverted ? Color.Black : Color.White);
        var family = fontFamily.Split(',')[0].Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(family)) family = "Arial";
        using var font = new Font(family, fontPx, style, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(inverted ? Color.White : Color.Black);
        var format = new StringFormat
        {
            Alignment = align,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
        };
        var pad = inverted ? 6f : 2f;
        var rect = new RectangleF(pad, 0, Math.Max(1, width - pad * 2), height);
        g.DrawString(text, font, brush, rect, format);
        return FromBitmap(bmp, inverted ? 148 : 172);
    }

    public static float FitFontPx(string text, int width, int height, float maxPx, string fontFamily, FontStyle style)
    {
        maxPx = Math.Max(6, maxPx);
        var family = fontFamily.Split(',')[0].Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(family)) family = "Arial";
        var pad = 4f;
        var maxW = Math.Max(4, width - pad);
        using var bmp = new Bitmap(8, 8);
        using var g = Graphics.FromImage(bmp);
        for (var px = maxPx; px >= 6; px -= 0.5f)
        {
            using var font = new Font(family, px, style, GraphicsUnit.Pixel);
            var size = g.MeasureString(text, font, (int)maxW);
            if (size.Width <= maxW + 1 && size.Height <= height + 1)
                return px;
        }
        return 6;
    }

    public static string FromPngBytes(byte[] png, int width, int height)
    {
        using var ms = new MemoryStream(png);
        using var src = new Bitmap(ms);
        using var bmp = new Bitmap(Math.Max(1, width), Math.Max(1, height), PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        var scale = Math.Min((float)bmp.Width / src.Width, (float)bmp.Height / src.Height);
        var dw = src.Width * scale;
        var dh = src.Height * scale;
        g.DrawImage(src, (bmp.Width - dw) / 2, (bmp.Height - dh) / 2, dw, dh);
        return FromBitmap(bmp, 186);
    }
}
