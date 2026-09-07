using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Spil.LabelPrint.Service.Compilation;

/// <summary>
/// Rasterize compiled ZPL graphics, then wrap as the selected printer language.
/// Zebra / Honeywell / Citizen / SATO-SZPL stay native ZPL; TSC, Godex, SATO SBPL,
/// and Datamax get a full-label graphic in their own command language (ASCII hex).
/// </summary>
internal static class LabelJobEncoder
{
    public static string Encode(string zpl, string language, double widthMm, double heightMm, int dpmm)
    {
        language = (language ?? "zpl").Trim().ToLowerInvariant();
        if (language is "zpl" or "" || string.IsNullOrWhiteSpace(zpl))
            return zpl;

        var page = ZplRaster.Render(zpl, dpmm, widthMm, heightMm);
        return language switch
        {
            "tspl" => ToTspl(page, widthMm, heightMm),
            "ezpl" => ToEzpl(page, widthMm, heightMm),
            "sbpl" => ToSbpl(page),
            "dpl" => ToDpl(page),
            "epl" => ToEpl(page),
            _ => zpl,
        };
    }

    static string ToTspl(ZplRaster.Page page, double widthMm, double heightMm)
    {
        var sb = new StringBuilder(page.Hex.Length + 256);
        sb.AppendLine(Inv("SIZE {0:0.##} mm,{1:0.##} mm", widthMm, heightMm));
        sb.AppendLine("GAP 3 mm,0 mm");
        sb.AppendLine("DIRECTION 1");
        sb.AppendLine("DENSITY 8");
        sb.AppendLine("CLS");
        sb.AppendLine(Inv("BITMAP 0,0,{0},{1},0,{2}", page.BytesPerRow, page.Height, page.Hex));
        sb.AppendLine("PRINT 1,1");
        return sb.ToString();
    }

    static string ToEzpl(ZplRaster.Page page, double widthMm, double heightMm)
    {
        var sb = new StringBuilder(page.Hex.Length + 256);
        sb.AppendLine(Inv("^Q{0:0.##},3", heightMm));
        sb.AppendLine(Inv("^W{0:0.##}", widthMm));
        sb.AppendLine("^H15");
        sb.AppendLine("^P1");
        sb.AppendLine("^S3");
        sb.AppendLine("^L");
        sb.AppendLine("Dy2");
        sb.AppendLine(Inv("~DGimg,{0},{1},{2}", page.TotalBytes, page.BytesPerRow, page.Hex));
        sb.AppendLine("Y0,0,img");
        sb.AppendLine("E");
        return sb.ToString();
    }

    static string ToSbpl(ZplRaster.Page page)
    {
        const char esc = '\u001b';
        var sb = new StringBuilder(page.Hex.Length + 128);
        sb.Append(esc).Append('A').Append('\r').Append('\n');
        sb.Append(esc).Append("V0");
        sb.Append(esc).Append("H0").Append('\r').Append('\n');
        sb.Append(esc).Append("GH");
        sb.Append(page.BytesPerRow.ToString("0000", CultureInfo.InvariantCulture));
        sb.Append(page.Height.ToString("0000", CultureInfo.InvariantCulture));
        sb.Append(page.Hex);
        sb.Append('\r').Append('\n');
        sb.Append(esc).Append("Q1").Append('\r').Append('\n');
        sb.Append(esc).Append('Z').Append('\r').Append('\n');
        return sb.ToString();
    }

    static string ToDpl(ZplRaster.Page page)
    {
        var sb = new StringBuilder(page.Hex.Length + 128);
        sb.Append('\u0002').Append("L\r");
        sb.Append("D11\r");
        sb.Append(Inv("Q{0}\r", page.Height));
        sb.Append(Inv("q{0}\r", page.Width));
        sb.Append(Inv("u00000{0:000}{1:0000}{2}\r", Math.Min(page.BytesPerRow, 999), Math.Min(page.Height, 9999), page.Hex));
        sb.Append("E\r");
        return sb.ToString();
    }

    static string ToEpl(ZplRaster.Page page)
    {
        var sb = new StringBuilder(page.Hex.Length + 128);
        sb.AppendLine("N");
        sb.AppendLine(Inv("q{0}", page.Width));
        sb.AppendLine(Inv("Q{0},24", page.Height));
        sb.AppendLine(Inv("GW0,0,{0},{1},{2}", page.BytesPerRow, page.Height, page.Hex));
        sb.AppendLine("P1");
        return sb.ToString();
    }

    static string Inv(string format, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, format, args);
}

/// <summary>Paint ^GFA / ^GB / ^GC / Code 128 from a ZPL job onto a 1-bit page.</summary>
internal static class ZplRaster
{
    public sealed class Page
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public int BytesPerRow { get; init; }
        public int TotalBytes => BytesPerRow * Height;
        public string Hex { get; init; } = "";
    }

    static readonly string[] C128 =
        ("11011001100 11001101100 11001100110 10010011000 10010001100 10001001100 " +
         "10011001000 10011000100 10001100100 11001001000 11001000100 11000100100 " +
         "10110011100 10011011100 10011001110 10111001100 10011101100 10011100110 " +
         "11001110010 11001011100 11001001110 11011100100 11001110100 11101101110 " +
         "11101001100 11100101100 11100100110 11101100100 11100110100 11100110010 " +
         "11011011000 11011000110 11000110110 10100011000 10001011000 10001000110 " +
         "10110001000 10001101000 10001100010 11010001000 11000101000 11000100010 " +
         "10110111000 10110001110 10001101110 10111011000 10111000110 10001110110 " +
         "11101110110 11010001110 11000101110 11011101000 11011100010 11011101110 " +
         "11101011000 11101000110 11100010110 11101101000 11101100010 11100011010 " +
         "11101111010 11001000010 11110001010 10100110000 10100001100 10010110000 " +
         "10010000110 10000101100 10000100110 10110010000 10110000100 10011010000 " +
         "10011000010 10000110100 10000110010 11000010010 11001010000 11110111010 " +
         "11000010100 10001111010 10100111100 10010111100 10010011110 10111100100 " +
         "10011110100 10011110010 11110100100 11110010100 11110010010 11011011110 " +
         "11011110110 11110110110 10101111000 10100011110 10001011110 10111101000 " +
         "10111100010 11110101000 11110100010 10111011110 10111101110 11101011110 " +
         "11110101110 11010001100 11001001100 11001000110 11000111010").Split(' ');

    public static Page Render(string zpl, int dpmm, double widthMm, double heightMm)
    {
        var text = zpl ?? "";
        var pw = MatchInt(text, @"\^PW(\d+)", Math.Max(8, (int)Math.Round(widthMm * dpmm)));
        var ll = MatchInt(text, @"\^LL(\d+)", Math.Max(8, (int)Math.Round(heightMm * dpmm)));
        pw = Math.Clamp(pw, 8, 4800);
        ll = Math.Clamp(ll, 8, 7200);
        var bits = new bool[pw * ll];

        foreach (Match m in Regex.Matches(text, @"\^FO(\d+),(\d+)\^GFA,(\d+),(\d+),(\d+),([0-9A-Fa-f]+)\^FS", RegexOptions.IgnoreCase))
        {
            BlitGfa(bits, pw, ll, Int(m, 1), Int(m, 2), Int(m, 5), m.Groups[6].Value);
        }

        foreach (Match m in Regex.Matches(text, @"\^FO(\d+),(\d+)\^GB(\d+),(\d+),(\d+)(?:,([Bb]))?\^FS", RegexOptions.IgnoreCase))
        {
            FillGb(bits, pw, ll, Int(m, 1), Int(m, 2), Int(m, 3), Int(m, 4), Int(m, 5), m.Groups[6].Success);
        }

        foreach (Match m in Regex.Matches(text, @"\^FO(\d+),(\d+)\^GC(\d+),(\d+)", RegexOptions.IgnoreCase))
        {
            StrokeCircle(bits, pw, ll, Int(m, 1), Int(m, 2), Int(m, 3), Math.Max(1, Int(m, 4)));
        }

        foreach (Match m in Regex.Matches(
                     text,
                     @"\^FO(\d+),(\d+)\^BY(\d+),[\d.]+,(\d+)\s*\^BC[A-Z]?,(\d+),[YN],[YN],[YN]\s*\^FD(.*?)\^FS",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            DrawCode128(bits, pw, ll, Int(m, 1), Int(m, 2), Math.Max(1, Int(m, 3)), Int(m, 5), m.Groups[6].Value);
        }

        var bpr = (pw + 7) / 8;
        var packed = new byte[bpr * ll];
        for (var y = 0; y < ll; y++)
        {
            for (var x = 0; x < pw; x++)
            {
                if (!bits[y * pw + x]) continue;
                packed[y * bpr + (x >> 3)] |= (byte)(0x80 >> (x & 7));
            }
        }

        var hex = new StringBuilder(packed.Length * 2);
        foreach (var b in packed)
            hex.Append(b.ToString("X2", CultureInfo.InvariantCulture));

        return new Page
        {
            Width = pw,
            Height = ll,
            BytesPerRow = bpr,
            Hex = hex.ToString(),
        };
    }

    static int MatchInt(string text, string pattern, int fallback)
    {
        var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : fallback;
    }

    static int Int(Match m, int i) => int.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture);

    static void Plot(bool[] bits, int w, int h, int x, int y)
    {
        if ((uint)x < (uint)w && (uint)y < (uint)h)
            bits[y * w + x] = true;
    }

    static void BlitGfa(bool[] bits, int w, int h, int ox, int oy, int bpr, string hex)
    {
        if (bpr <= 0 || string.IsNullOrEmpty(hex)) return;
        if (hex.Length % 2 == 1) hex += "0";
        var raw = new byte[hex.Length / 2];
        for (var i = 0; i < raw.Length; i++)
            raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        var rows = (raw.Length + bpr - 1) / bpr;
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < bpr * 8; x++)
            {
                var bi = y * bpr + (x >> 3);
                if (bi >= raw.Length) break;
                if ((raw[bi] & (0x80 >> (x & 7))) != 0)
                    Plot(bits, w, h, ox + x, oy + y);
            }
        }
    }

    static void FillGb(bool[] bits, int w, int h, int ox, int oy, int bw, int bh, int thick, bool filled)
    {
        if (bw <= 0 || bh <= 0) return;
        if (filled || thick >= Math.Min(bw, bh))
        {
            for (var y = oy; y < oy + bh; y++)
            for (var x = ox; x < ox + bw; x++)
                Plot(bits, w, h, x, y);
            return;
        }
        var t = Math.Max(1, thick);
        for (var i = 0; i < t; i++)
        {
            for (var x = ox; x < ox + bw; x++)
            {
                Plot(bits, w, h, x, oy + i);
                Plot(bits, w, h, x, oy + bh - 1 - i);
            }
            for (var y = oy; y < oy + bh; y++)
            {
                Plot(bits, w, h, ox + i, y);
                Plot(bits, w, h, ox + bw - 1 - i, y);
            }
        }
    }

    static void StrokeCircle(bool[] bits, int w, int h, int ox, int oy, int d, int thick)
    {
        var r = Math.Max(1, d / 2);
        var cx = ox + r;
        var cy = oy + r;
        for (var y = oy; y <= oy + d; y++)
        for (var x = ox; x <= ox + d; x++)
        {
            var dx = x - cx;
            var dy = y - cy;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            if (Math.Abs(dist - r) <= Math.Max(1, thick))
                Plot(bits, w, h, x, y);
        }
    }

    static void DrawCode128(bool[] bits, int w, int h, int ox, int oy, int module, int barH, string value)
    {
        var pattern = Code128Bits(value ?? "");
        var x = ox;
        foreach (var bit in pattern)
        {
            if (bit == '1')
            {
                for (var dy = 0; dy < barH; dy++)
                for (var dx = 0; dx < module; dx++)
                    Plot(bits, w, h, x + dx, oy + dy);
            }
            x += module;
        }
    }

    static string Code128Bits(string value)
    {
        var checksum = 104;
        var bits = new StringBuilder(C128[104]);
        for (var i = 0; i < value.Length; i++)
        {
            var idx = value[i] - 32;
            if (idx < 0 || idx > 94) idx = 0;
            bits.Append(C128[idx]);
            checksum += idx * (i + 1);
        }
        bits.Append(C128[checksum % 103]);
        if (C128.Length > 106) bits.Append(C128[106]);
        else bits.Append("1100011101011");
        bits.Append("11");
        return bits.ToString();
    }
}
