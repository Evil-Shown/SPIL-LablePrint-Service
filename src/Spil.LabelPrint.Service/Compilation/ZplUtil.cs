using System.Globalization;
using System.Text;

namespace Spil.LabelPrint.Service.Compilation;

internal static class ZplUtil
{
    public static string Sanitize(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c is '^' or '~') sb.Append(' ');
            else if (c is '\r') { }
            else sb.Append(c);
        }
        return sb.ToString();
    }

    public static bool IsEmpty(string? v)
    {
        var s = (v ?? "").Trim();
        return s.Length == 0 || s is "-" or "—" or "–" or "***" or "#####";
    }

    public static int ParseDpmm(int? dpmm, int? printerDpi)
    {
        if (dpmm is 6 or 8 or 12 or 24) return dpmm.Value;
        var dpi = printerDpi ?? 300;
        if (dpi >= 500) return 24;
        if (dpi >= 250) return 12;
        if (dpi >= 180) return 8;
        return 6;
    }

    public static string NormalizeBrand(string? raw)
    {
        return ResolveBrand(raw).Brand;
    }

    public static string NormalizeLayout(string? raw)
    {
        var v = (raw ?? "template").Trim().ToLowerInvariant();
        return v is "metro" or "erp-metro" ? "metro" : "template";
    }

    public static string NormalizeClient(string? raw)
    {
        var v = (raw ?? "").Trim().ToLowerInvariant().Replace('_', '-');
        return v switch
        {
            "opti" or "spil-opti" or "opti-tv" or "tv" => "opti",
            "erp" => "erp",
            _ => v,
        };
    }

    public static (string Brand, string Language) ResolveBrand(string? raw)
    {
        var v = (raw ?? "zebra").Trim().ToLowerInvariant().Replace('_', '-');
        return v switch
        {
            "honeywell" or "intermec" => ("honeywell", "zpl"),
            "citizen" => ("citizen", "zpl"),
            "sato" or "sato-szpl" => ("sato", "zpl"),
            "sato-sbpl" or "sato-native" => ("sato-sbpl", "sbpl"),
            "tsc" or "tspl" => ("tsc", "tspl"),
            "godex" or "ezpl" => ("godex", "ezpl"),
            "datamax" or "dpl" => ("datamax", "dpl"),
            "epl" or "eltron" => ("epl", "epl"),
            _ => ("zebra", "zpl"),
        };
    }

    public static readonly string[] Brands =
    {
        "zebra", "honeywell", "citizen", "sato", "sato-sbpl", "tsc", "godex", "datamax", "epl",
    };

    public static readonly string[] Languages = { "zpl", "tspl", "ezpl", "sbpl", "dpl", "epl" };

    public static readonly PrinterBrandInfo[] PrinterCatalog =
    {
        new("zebra", "zpl", "Zebra", "Native ZPL on TCP 9100"),
        new("honeywell", "zpl", "Honeywell", "Enable ZPL or ZSim on the printer"),
        new("citizen", "zpl", "Citizen", "Enable Zebra ZPL emulation"),
        new("sato", "zpl", "SATO (SZPL)", "Enable SZPL / ZPL emulation"),
        new("sato-sbpl", "sbpl", "SATO (SBPL)", "Native SBPL"),
        new("tsc", "tspl", "TSC", "Native TSPL (TE / TX / TTP)"),
        new("godex", "ezpl", "Godex", "Native EZPL"),
        new("datamax", "dpl", "Datamax", "Native DPL (I-Class / A-Class)"),
        new("epl", "epl", "Eltron / EPL2", "Older EPL2 printers"),
    };

    public sealed record PrinterBrandInfo(string Brand, string Language, string Name, string Hint);

    public static string Fmt(string format, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, format, args);

    /// <summary>
    /// Zebra/Honeywell reuse the last reprint count when ^PQ is omitted.
    /// Two labels with a stored count of 3 become six physical copies.
    /// </summary>
    public static string ForceOneCopy(string zpl)
    {
        if (string.IsNullOrEmpty(zpl) ||
            zpl.IndexOf("^XA", StringComparison.OrdinalIgnoreCase) < 0)
            return zpl ?? "";

        var sb = new StringBuilder(zpl.Length + 32);
        for (var i = 0; i < zpl.Length; i++)
        {
            if (zpl[i] == '^' && i + 2 < zpl.Length)
            {
                var a = char.ToUpperInvariant(zpl[i + 1]);
                var b = char.ToUpperInvariant(zpl[i + 2]);
                if (a == 'P' && b == 'Q')
                {
                    i += 3;
                    while (i < zpl.Length && zpl[i] is not '^' and not '\n' and not '\r')
                        i++;
                    i--;
                    continue;
                }
                if (a == 'X' && b == 'Z')
                {
                    sb.Append("^PQ1\n^XZ");
                    i += 2;
                    continue;
                }
            }
            sb.Append(zpl[i]);
        }
        return sb.ToString();
    }
}
