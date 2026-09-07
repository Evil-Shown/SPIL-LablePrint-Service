using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Spil.LabelPrint.Service.Compilation;

/// <summary>
/// Compiles any Opti Labels-editor JSON (e.g. LBL_004.json) — CSS px @ 96 DPI — to ZPL.
/// ERP can POST the same template object; field values come from labelData + fieldMappings.
/// </summary>
public static class TemplateZplCompiler
{
    private const double DesignDpi = 96;

    public static string Compile(JsonElement template, JsonElement? labelData, JsonElement? configuration, int dpmm, JsonElement? project = null)
    {
        template = UnwrapTemplate(template);
        if (template.ValueKind != JsonValueKind.Object || !HasFields(template))
            throw new ArgumentException("template must be Opti Labels JSON with sections (or a root fields array), same shape as LBL_004.json.");

        var dotsPerPx = (dpmm * 25.4) / DesignDpi;
        var layout = ContentLayout(template, configuration);
        int ToDots(double px) => Math.Max(0, (int)Math.Round(px * dotsPerPx));
        int MapX(double px) => ToDots(layout.ContentLeft + px * layout.FitScale);
        int MapY(double px) => ToDots(layout.ContentTop + px * layout.FitScale);
        int MapS(double px) => Math.Max(1, ToDots(px * layout.FitScale));

        var pw = Math.Max(1, (int)Math.Round(layout.WidthMm * dpmm));
        var ll = Math.Max(1, (int)Math.Round(layout.HeightMm * dpmm));
        var lookup = new LabelFieldResolver(template, labelData, configuration, project);
        var globalFamily = Family(Str(Get(Get(template, "globalStyles"), "fontFamily"), "Arial"));

        var sb = new StringBuilder(4096);
        sb.AppendLine("^XA");
        sb.AppendLine("^CI28");
        sb.AppendLine(Inv("^PW{0}", pw));
        sb.AppendLine(Inv("^LL{0}", ll));
        sb.AppendLine("^LH0,0");
        sb.AppendLine("^LS0");
        sb.AppendLine("^PON");
        sb.AppendLine("^PQ1");

        foreach (var field in CollectFields(template))
        {
            var type = Str(Get(field, "type"), "text").ToLowerInvariant();
            var x = Num(Get(field, "x"), Num(Get(field, "left")));
            var y = Num(Get(field, "y"), Num(Get(field, "top")));
            var w = Math.Max(1, Num(Get(field, "width"), 10));
            var h = Math.Max(1, Num(Get(field, "height"), 10));
            var fx = MapX(x);
            var fy = MapY(y);
            var fw = MapS(w);
            var fh = MapS(h);
            var key = Str(Get(field, "fieldKey"));

            if (type == "line")
            {
                var strokeCss = Num(Get(field, "strokeWidth"), 1);
                var stroke = strokeCss <= 1 ? 1 : Math.Max(1, (int)Math.Round(MapS(strokeCss) * 0.42));
                sb.AppendLine(Inv("^FO{0},{1}^GB{2},{3},{4}^FS", fx, fy, fw, stroke, stroke));
                continue;
            }

            if (type == "shape")
            {
                var shape = Str(Get(field, "shapeType"), "rect").ToLowerInvariant();
                var stroke = Math.Max(1, MapS(Num(Get(field, "strokeWidth"), 2)));
                if (shape == "circle")
                {
                    var d = Math.Min(fw, fh);
                    sb.AppendLine(Inv("^FO{0},{1}^GC{2},{3}^FS", fx, fy, d, stroke));
                }
                else if (shape != "dxf")
                {
                    sb.AppendLine(Inv("^FO{0},{1}^GB{2},{3},{4}^FS", fx, fy, fw, fh, stroke));
                }
                else
                {
                    var oif = PickOifPath(labelData, configuration);
                    oif = lookup.Tokens(oif);
                    if (TryImageBytes(oif, out var png))
                    {
                        try { sb.AppendLine($"^FO{fx},{fy}{GfaEncoder.FromPngBytes(png, fw, fh)}^FS"); }
                        catch { /* skip */ }
                    }
                }
                continue;
            }

            if (type == "image")
            {
                var src = lookup.Tokens(Str(Get(field, "src"), Str(Get(field, "imagePath"), Str(Get(field, "assetPath")))));
                if (src.Contains("{{", StringComparison.Ordinal)) continue;
                if (TryImageBytes(src, out var png))
                {
                    try { sb.AppendLine($"^FO{fx},{fy}{GfaEncoder.FromPngBytes(png, fw, fh)}^FS"); }
                    catch { /* skip broken image */ }
                }
                continue;
            }

            if (type == "barcode")
            {
                var sources = LabelFieldResolver.StringList(field, "source");
                if (sources.Count == 0) sources.AddRange(new[] { "Barcode", "barcode" });
                var value = ZplUtil.Sanitize(lookup.Mapped(string.IsNullOrEmpty(key) ? "Barcode" : key, sources));
                if (ZplUtil.IsEmpty(value))
                    value = ZplUtil.Sanitize(lookup.Tokens(Str(Get(field, "value"), Str(Get(field, "fallbackValue")))));
                if (ZplUtil.IsEmpty(value)) continue;
                var showText = Get(field, "displayValue").ValueKind != JsonValueKind.False;
                var fontDots = Math.Max(6, MapS(Num(Get(field, "fontSize"), 8)));
                var interpH = showText ? fontDots + Math.Max(3, (int)Math.Round(fontDots * 0.4)) : 0;
                var barH = Math.Max(16, fh - interpH);
                var bits = Math.Max(1, value.Length * 11 + 24);
                var module = Math.Max(1, fw / bits);
                var barW = Math.Min(fw, bits * module);
                var barX = Math.Max(0, (fw - barW) / 2);
                var ori = ZplOrientation(Num(Get(field, "rotation"), Num(Get(field, "rotate"), Num(Get(field, "BrRotate")))));
                sb.AppendLine(Inv("^FO{0},{1}^BY{2},2,{3}", fx + barX, fy, module, barH));
                sb.AppendLine(Inv("^BC{0},{1},N,N,N", ori, barH));
                sb.AppendLine(Inv("^FD{0}^FS", value));
                if (showText)
                {
                    var gfa = GfaEncoder.TextField(value, barW, interpH, fontDots, "Arial", FontStyle.Regular, StringAlignment.Center, false);
                    sb.AppendLine($"^FO{fx + barX},{fy + barH}{gfa}^FS");
                }
                continue;
            }

            if (type == "machineschecklist" || key == "machines")
            {
                var raw = lookup.Mapped(string.IsNullOrEmpty(key) ? "machines" : key, LabelFieldResolver.StringList(field, "source"));
                if (ZplUtil.IsEmpty(raw)) raw = lookup.Tokens(Str(Get(field, "value")));
                if (ZplUtil.IsEmpty(raw)) continue;
                var items = raw.Split('>', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (items.Length == 0) continue;
                var machinesCssFont = Num(Get(field, "fontSize"), 11);
                var fontH = Math.Max(6, MapS(machinesCssFont));
                var box = Math.Max(MapS(8), MapS(machinesCssFont * 0.85));
                var rowGap = Math.Max(1, MapS(3));
                var itemGap = Math.Max(1, MapS(4));
                var stroke = Math.Max(1, MapS(1.5));
                var right = Str(Get(field, "textAlign")).Equals("right", StringComparison.OrdinalIgnoreCase);
                var gfa = GfaEncoder.MachinesChecklist(items, fw, fh, fontH, box, rowGap, itemGap, stroke, right);
                sb.AppendLine($"^FO{fx},{fy}{gfa}^FS");
                continue;
            }

            if (type is "qrcode")
            {
                var qr = ZplUtil.Sanitize(lookup.Mapped(string.IsNullOrEmpty(key) ? "qrcode" : key, LabelFieldResolver.StringList(field, "source")));
                if (ZplUtil.IsEmpty(qr)) qr = ZplUtil.Sanitize(lookup.Tokens(Str(Get(field, "value"), Str(Get(field, "fallbackValue")))));
                if (ZplUtil.IsEmpty(qr)) continue;
                var mag = Math.Clamp((int)Math.Round(Math.Min(fw, fh) / 25.0), 1, 10);
                var ori = ZplOrientation(Num(Get(field, "rotation"), Num(Get(field, "rotate"))));
                sb.AppendLine(Inv("^FO{0},{1}^BQ{2},2,{3}^FDQA,{4}^FS", fx, fy, ori, mag, qr));
                continue;
            }

            // text, header, and any unknown textual type
            var text = lookup.PrintText(field);
            if (ZplUtil.IsEmpty(text)) continue;
            text = ZplUtil.Sanitize(text);
            var inverted = IsDarkFill(field) || lookup.MappingIsBlackBox(key);
            var cssFont = Num(Get(field, "fontSize"), 12);
            var family = Family(Str(Get(field, "fontFamily"), globalFamily));
            var style = FontStyle.Regular;
            var weight = Str(Get(field, "fontWeight"));
            if (weight.Equals("bold", StringComparison.OrdinalIgnoreCase) ||
                (int.TryParse(weight, out var wn) && wn >= 600))
                style |= FontStyle.Bold;
            if (Str(Get(field, "fontStyle")).Equals("italic", StringComparison.OrdinalIgnoreCase))
                style |= FontStyle.Italic;
            var align = Str(Get(field, "textAlign")).ToLowerInvariant() switch
            {
                "center" or "middle" => StringAlignment.Center,
                "right" => StringAlignment.Far,
                _ => StringAlignment.Near,
            };
            var autoShrink = Get(field, "autoShrink").ValueKind != JsonValueKind.False;
            var fittedCss = autoShrink
                ? GfaEncoder.FitFontPx(text, Math.Max(1, (int)Math.Round(w)), Math.Max(1, (int)Math.Round(h)), (float)cssFont, family, style)
                : (float)cssFont;
            var fontH2 = Math.Max(8, MapS(fittedCss));
            var lineEm = LineHeightEm(Get(field, "lineHeight"), 1.12);
            var gfaText = GfaEncoder.TextField(text, fw, fh, fontH2, family, style, align, inverted, lineEm);
            sb.AppendLine($"^FO{fx},{fy}{gfaText}^FS");
        }

        sb.AppendLine("^XZ");
        return sb.ToString();
    }

    /// <summary>Accept LBL_004.json as-is, or wrapped as { template }, { data }, or a JSON string.</summary>
    public static JsonElement UnwrapTemplate(JsonElement template)
    {
        if (template.ValueKind == JsonValueKind.String)
        {
            var s = template.GetString();
            if (string.IsNullOrWhiteSpace(s)) return default;
            return JsonSerializer.Deserialize<JsonElement>(s);
        }
        if (template.ValueKind != JsonValueKind.Object) return template;
        if (HasFields(template)) return template;
        foreach (var wrap in new[] { "template", "data", "labelTemplate", "Template" })
        {
            if (template.TryGetProperty(wrap, out var inner) ||
                TryPropIgnore(template, wrap, out inner))
            {
                var u = UnwrapTemplate(inner);
                if (HasFields(u)) return u;
            }
        }
        return template;
    }

    public static bool HasFields(JsonElement template)
    {
        if (template.ValueKind != JsonValueKind.Object) return false;
        if (template.TryGetProperty("fields", out var root) && root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            return true;
        if (!template.TryGetProperty("sections", out var sections)) return false;
        if (sections.ValueKind == JsonValueKind.Array)
        {
            foreach (var sec in sections.EnumerateArray())
                if (sec.ValueKind == JsonValueKind.Object && sec.TryGetProperty("fields", out var f) && f.ValueKind == JsonValueKind.Array && f.GetArrayLength() > 0)
                    return true;
        }
        if (sections.ValueKind == JsonValueKind.Object)
        {
            foreach (var sec in sections.EnumerateObject())
            {
                var v = sec.Value;
                if (v.ValueKind == JsonValueKind.Object && v.TryGetProperty("fields", out var f) && f.ValueKind == JsonValueKind.Array && f.GetArrayLength() > 0)
                    return true;
            }
        }
        return false;
    }

    private static (double WidthMm, double HeightMm, double FitScale, double ContentLeft, double ContentTop)
        ContentLayout(JsonElement template, JsonElement? configuration)
    {
        double MmToPx(double mm) => mm * DesignDpi / 25.4;
        var widthMm = Num(Get(template, "width"), 100);
        var heightMm = Num(Get(template, "height"), 150);
        var labels = configuration is { ValueKind: JsonValueKind.Object } cfg && cfg.TryGetProperty("labels", out var l)
            ? l
            : default;
        var padL = MmToPx(Num(Get(labels, "left"), Num(Get(template, "left"))));
        var padT = MmToPx(Num(Get(labels, "top"), Num(Get(template, "top"))));
        var padR = MmToPx(Num(Get(labels, "right"), Num(Get(template, "right"))));
        var padB = MmToPx(Num(Get(labels, "bottom"), Num(Get(template, "bottom"))));
        var outerW = MmToPx(widthMm);
        var outerH = MmToPx(heightMm);
        var innerW = Math.Max(1, outerW - padL - padR);
        var innerH = Math.Max(1, outerH - padT - padB);
        var hasMargin = padL > 0 || padT > 0 || padR > 0 || padB > 0;
        var fit = hasMargin ? Math.Min(innerW / outerW, innerH / outerH) : 1;
        var contentLeft = padL + (innerW - outerW * fit) / 2;
        var contentTop = padT + (innerH - outerH * fit) / 2;
        return (widthMm, heightMm, fit, contentLeft, contentTop);
    }

    private static IEnumerable<JsonElement> CollectFields(JsonElement template)
    {
        var list = new List<(int Rank, JsonElement Field)>();
        void AddField(JsonElement f)
        {
            var t = Str(Get(f, "type"), "text").ToLowerInvariant();
            var rank = t is "line" or "shape" or "image" ? 0 : t is "barcode" or "qrcode" ? 1 : 2;
            list.Add((rank, f));
        }

        var hasSections = template.TryGetProperty("sections", out var sections) &&
                          sections.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        if (!hasSections && template.TryGetProperty("fields", out var rootFields) && rootFields.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in rootFields.EnumerateArray()) AddField(f);
        }

        if (hasSections)
        {
            IEnumerable<JsonElement> secs = sections.ValueKind == JsonValueKind.Array
                ? sections.EnumerateArray()
                : sections.ValueKind == JsonValueKind.Object
                    ? sections.EnumerateObject().Select(p => p.Value)
                    : Enumerable.Empty<JsonElement>();
            foreach (var v in secs)
            {
                if (v.ValueKind != JsonValueKind.Object) continue;
                if (v.TryGetProperty("enabled", out var en) && en.ValueKind == JsonValueKind.False) continue;
                if (!v.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Array) continue;
                foreach (var f in fields.EnumerateArray()) AddField(f);
            }
        }

        foreach (var f in list.OrderBy(x => x.Rank))
            yield return f.Field;
    }

    private static bool IsDarkFill(JsonElement field)
    {
        var bg = Str(Get(field, "backgroundColor")).ToLowerInvariant();
        var fg = Str(Get(field, "color")).ToLowerInvariant();
        var black = bg is "#000" or "#000000" or "black";
        var white = fg is "#fff" or "#ffffff" or "white";
        if (Get(field, "isBlackBox").ValueKind == JsonValueKind.True) return true;
        return black && white;
    }

    private static string PickOifPath(JsonElement? labelData, JsonElement? configuration)
    {
        var source = "";
        if (configuration is { ValueKind: JsonValueKind.Object } cfg &&
            cfg.TryGetProperty("labels", out var labels) && labels.ValueKind == JsonValueKind.Object)
            source = Str(Get(labels, "shapeImageSource")).ToLowerInvariant();
        var labelPng = Str(Get(labelData ?? default, "labelPng"), Str(Get(labelData ?? default, "labelPNG")));
        var png = Str(Get(labelData ?? default, "png"));
        if (source.Contains("label")) return labelPng;
        if (source == "png") return png;
        return string.IsNullOrEmpty(labelPng) ? png : labelPng;
    }

    private static bool TryImageBytes(string src, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(src)) return false;
        const string marker = "base64,";
        var i = src.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (i >= 0)
        {
            try
            {
                bytes = Convert.FromBase64String(src[(i + marker.Length)..]);
                return bytes.Length > 0;
            }
            catch { return false; }
        }
        try
        {
            var path = src;
            if (path.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                path = Uri.UnescapeDataString(path[8..]);
            else if (path.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                path = Uri.UnescapeDataString(path[7..]);
            if (path.Length >= 2 && File.Exists(path))
            {
                bytes = File.ReadAllBytes(path);
                return bytes.Length > 0;
            }
        }
        catch { /* ignore bad paths */ }
        return false;
    }

    private static double LineHeightEm(JsonElement el, double fallback)
    {
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var n) && n > 0)
            return n > 4 ? 1.12 : n;
        var s = Str(el).Trim().ToLowerInvariant();
        if (s.EndsWith("px", StringComparison.Ordinal) &&
            double.TryParse(s[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var px) && px > 0)
            return Math.Clamp(px / 12.0, 0.8, 2.0);
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var em) && em > 0 && em <= 4)
            return em;
        return fallback;
    }

    private static string Family(string fontFamily)
    {
        var family = (fontFamily ?? "Arial").Split(',')[0].Trim().Trim('"');
        return string.IsNullOrWhiteSpace(family) ? "Arial" : family;
    }

    private static string ZplOrientation(double rotation)
    {
        var r = ((rotation % 360) + 360) % 360;
        if (r >= 45 && r < 135) return "R";
        if (r >= 135 && r < 225) return "I";
        if (r >= 225 && r < 315) return "B";
        return "N";
    }

    private static bool TryPropIgnore(JsonElement obj, string name, out JsonElement value)
    {
        value = default;
        if (obj.ValueKind != JsonValueKind.Object) return false;
        foreach (var p in obj.EnumerateObject())
        {
            if (!string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
            value = p.Value;
            return true;
        }
        return false;
    }

    private static JsonElement Get(JsonElement obj, string name) => LabelFieldResolver.Get(obj, name);
    private static string Str(JsonElement el, string fallback = "") => LabelFieldResolver.Str(el, fallback);
    private static double Num(JsonElement el, double fallback = 0) => LabelFieldResolver.Num(el, fallback);
    private static string Inv(string format, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, format, args);
}
