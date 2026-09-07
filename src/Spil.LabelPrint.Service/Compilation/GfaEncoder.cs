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
        bool inverted,
        double lineHeightEm = 1.12)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        fontPx = Math.Max(6, fontPx);
        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(inverted ? Color.Black : Color.White);
        var family = fontFamily.Split(',')[0].Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(family)) family = "Arial";
        using var font = new Font(family, fontPx, style, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(inverted ? Color.White : Color.Black);
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.Alignment = align;
        format.LineAlignment = StringAlignment.Near;
        format.FormatFlags |= StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces;
        format.Trimming = StringTrimming.None;

        var pad = inverted ? 6f : 2f;
        var maxW = Math.Max(1, width - pad * 2);
        var lines = WrapLines(g, font, text, maxW, format);
        if (lines.Count == 0) return FromBitmap(bmp, inverted ? 148 : 172);

        var em = lineHeightEm > 0.5 && lineHeightEm < 4 ? lineHeightEm : 1.12;
        var lineH = Math.Max(fontPx, fontPx * (float)em);
        var blockH = lines.Count * lineH;
        var startY = Math.Max(0, (height - blockH) / 2f);

        for (var i = 0; i < lines.Count; i++)
        {
            var yy = startY + i * lineH;
            if (yy >= height) break;
            g.DrawString(lines[i], font, brush, new RectangleF(pad, yy, maxW, lineH), format);
        }
        return FromBitmap(bmp, inverted ? 148 : 172);
    }

    /// <summary>Same compact stacking as Opti HTML machines checklist — do not stretch rows to fill the field.</summary>
    public static string MachinesChecklist(
        IReadOnlyList<string> items,
        int width,
        int height,
        float fontPx,
        int boxPx,
        int rowGapPx,
        int itemGapPx,
        int strokePx,
        bool rightAlign)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        fontPx = Math.Max(6, fontPx);
        boxPx = Math.Max(6, boxPx);
        rowGapPx = Math.Max(1, rowGapPx);
        itemGapPx = Math.Max(1, itemGapPx);
        strokePx = Math.Max(1, strokePx);
        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.White);
        using var font = new Font("Arial", fontPx, FontStyle.Regular, GraphicsUnit.Pixel);
        using var pen = new Pen(Color.Black, strokePx);
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.Alignment = StringAlignment.Near;
        format.LineAlignment = StringAlignment.Center;
        format.FormatFlags |= StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces;

        var rowH = Math.Max(fontPx, boxPx);
        for (var i = 0; i < items.Count; i++)
        {
            var yy = i * (rowH + rowGapPx);
            if (yy >= height) break;
            var label = items[i] ?? "";
            var textSize = g.MeasureString(label, font, int.MaxValue, format);
            var textW = Math.Min(textSize.Width, Math.Max(8, width - boxPx - itemGapPx));
            float textX, boxX;
            if (rightAlign)
            {
                boxX = width - boxPx;
                textX = Math.Max(0, boxX - itemGapPx - textW);
            }
            else
            {
                textX = 0;
                boxX = Math.Min(width - boxPx, textX + textW + itemGapPx);
            }
            g.DrawString(label, font, Brushes.Black, new RectangleF(textX, yy, textW, rowH), format);
            var boxY = yy + Math.Max(0, (rowH - boxPx) / 2f);
            g.DrawRectangle(pen, boxX, boxY, boxPx, boxPx);
        }
        return FromBitmap(bmp, 172);
    }

    static List<string> WrapLines(Graphics g, Font font, string text, float maxW, StringFormat format)
    {
        var lines = new List<string>();
        var raw = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
        foreach (var para in raw.Split('\n'))
        {
            if (para.Length == 0)
            {
                lines.Add("");
                continue;
            }
            var cur = "";
            foreach (var word in para.Split(' '))
            {
                var trial = cur.Length == 0 ? word : cur + " " + word;
                var size = g.MeasureString(trial, font, new SizeF(maxW * 2, font.Size * 4), format);
                if (size.Width <= maxW || cur.Length == 0) cur = trial;
                else
                {
                    lines.Add(cur);
                    cur = word;
                }
            }
            if (cur.Length > 0) lines.Add(cur);
        }
        return lines;
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
            using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
            var size = g.MeasureString(text, font, (int)maxW, format);
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
