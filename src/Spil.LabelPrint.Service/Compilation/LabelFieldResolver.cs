using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Spil.LabelPrint.Service.Compilation;

/// <summary>
/// Same resolution order as Opti <c>labelFieldResolver.js</c> + template token / noteField mapping.
/// Works for any Labels-editor JSON (LBL_004 and ERP copies of the same shape).
/// </summary>
internal sealed class LabelFieldResolver
{
    private readonly JsonElement _labelData;
    private readonly JsonElement _template;
    private readonly JsonElement _configuration;
    private readonly JsonElement _project;

    public LabelFieldResolver(JsonElement template, JsonElement? labelData, JsonElement? configuration, JsonElement? project)
    {
        _template = template.ValueKind == JsonValueKind.Object ? template : default;
        _labelData = labelData is { ValueKind: JsonValueKind.Object } d ? d : default;
        _configuration = configuration is { ValueKind: JsonValueKind.Object } c ? c : default;
        _project = project is { ValueKind: JsonValueKind.Object } p ? p : default;
    }

    public string Tokens(string? value)
    {
        var str = value ?? "";
        if (!str.Contains("{{", StringComparison.Ordinal)) return str;
        return Regex.Replace(str, @"\{\{(.*?)\}\}", m =>
        {
            var resolved = ResolveProjectField(m.Groups[1].Value.Trim());
            return ZplUtil.IsEmpty(resolved) ? "" : resolved;
        });
    }

    public JsonElement? PreferredMapping(string? fieldKey)
    {
        if (string.IsNullOrWhiteSpace(fieldKey)) return null;
        if (TryMapping(_template, "fieldMappings", fieldKey, out var m)) return m;
        if (TryUsableLabelCfg(fieldKey, out m)) return m;
        return null;
    }

    public bool MappingIsBlackBox(string? fieldKey)
    {
        var m = PreferredMapping(fieldKey);
        return m is { ValueKind: JsonValueKind.Object } obj &&
               obj.TryGetProperty("isBlackBox", out var b) &&
               b.ValueKind == JsonValueKind.True;
    }

    public string Mapped(string? fieldKey, IEnumerable<string>? fallbackKeys)
    {
        var cfg = PreferredMapping(fieldKey);
        var nf = cfg is { } c ? Num(Get(c, "noteField")) : 0;
        var sf = cfg is { } c2 ? (int)Num(Get(c2, "subField")) : 0;

        if (nf > 0 && _labelData.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in new[] { $"note{nf}", $"Note{nf}" })
            {
                if (!_labelData.TryGetProperty(name, out var raw)) continue;
                var fromNote = ReadNote(raw, sf);
                if (!ZplUtil.IsEmpty(fromNote)) return Tokens(fromNote);
            }
        }

        foreach (var k in new[] { fieldKey }.Concat(fallbackKeys ?? Array.Empty<string>()))
        {
            if (string.IsNullOrWhiteSpace(k)) continue;
            var v = GetDataString(k);
            if (!ZplUtil.IsEmpty(v)) return Tokens(v);
        }
        return "";
    }

    public string PrintText(JsonElement field)
    {
        var templateValueStr = Str(Get(field, "value"));
        var fieldKey = Str(Get(field, "fieldKey"));
        var cfg = PreferredMapping(fieldKey);
        var hasNote = cfg is { } c && Num(Get(c, "noteField")) > 0;
        var hasTokens = templateValueStr.Contains("{{", StringComparison.Ordinal);
        var sources = StringList(field, "source");

        string value;
        if (hasTokens)
            value = templateValueStr;
        else if (hasNote)
        {
            var mapped = Mapped(fieldKey, sources);
            value = ZplUtil.IsEmpty(mapped) ? "" : mapped;
        }
        else if (!string.IsNullOrWhiteSpace(templateValueStr))
            value = templateValueStr;
        else
        {
            var mapped = Mapped(fieldKey, sources);
            value = ZplUtil.IsEmpty(mapped) ? "" : mapped;
        }

        value = Tokens(value);
        if (ZplUtil.IsEmpty(value)) value = "";

        if (!string.IsNullOrEmpty(value) && hasTokens)
        {
            var staticOnly = Regex.Replace(templateValueStr, @"\{\{.*?\}\}", "").Trim();
            if (staticOnly.Length > 0 && value.Trim() == staticOnly) value = "";
        }
        return value;
    }

    public string ResolveProjectField(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "";
        var nk = NormalizeKey(key);
        var sub = FirstObject(Get(_project, "subProjects"));
        var sheet = FirstObject(Get(sub, "sheets"));
        var parameters = Get(_project, "parameters");

        string Pick(params string[] vals)
        {
            foreach (var v in vals)
                if (!ZplUtil.IsEmpty(v)) return v;
            return "";
        }

        if (nk is "description" or "glassdescription")
            return Pick(Str(Get(sheet, "glassDescription")), Str(Get(sheet, "descriptionSearch")));
        if (nk == "size")
        {
            var w = Pick(Str(Get(sheet, "disWidth")), Str(Get(sheet, "width")));
            var h = Pick(Str(Get(sheet, "disHeight")), Str(Get(sheet, "height")));
            var t = Str(Get(sheet, "thickness"));
            if (ZplUtil.IsEmpty(w) && ZplUtil.IsEmpty(h) && ZplUtil.IsEmpty(t)) return "";
            return ZplUtil.IsEmpty(t) ? $"{ToInt(w)} x {ToInt(h)}" : $"{ToInt(w)} x {ToInt(h)} x {t}".Trim();
        }
        if (nk == "width")
        {
            var ld = Str(Get(_labelData, "width"));
            if (!ZplUtil.IsEmpty(ld)) return ToInt(ld);
            var v = Pick(Str(Get(sheet, "disWidth")), Str(Get(sheet, "width")));
            if (!ZplUtil.IsEmpty(v)) return ToInt(v);
        }
        if (nk == "height")
        {
            var ld = Str(Get(_labelData, "height"));
            if (!ZplUtil.IsEmpty(ld)) return ToInt(ld);
            var v = Pick(Str(Get(sheet, "disHeight")), Str(Get(sheet, "height")));
            if (!ZplUtil.IsEmpty(v)) return ToInt(v);
        }
        if (nk == "thickness") return Str(Get(sheet, "thickness"));
        if (nk == "itemcode") return Str(Get(sheet, "itemCode"));
        if (nk == "glasscode") return Str(Get(sheet, "glassCode"));
        if (nk == "glassfamily") return Str(Get(sub, "glassFamily"));
        if (nk == "projectname") return Str(Get(parameters, "projectName"));
        if (nk == "projectid") return Str(Get(parameters, "projectId"));
        if (nk is "batchnumbe" or "batchnumber" or "batchno" or "batchnum")
        {
            foreach (var v in new[]
                     {
                         FormatBatch(Get(parameters, "batchNumber")),
                         FormatBatch(Get(parameters, "batchNumbe")),
                         GetDataString("batchNumber"),
                         GetDataString("batchNumbe"),
                         GetDataString("BatchNum"),
                         GetDataString("BatchNo"),
                     })
                if (!ZplUtil.IsEmpty(v)) return v;
            return "";
        }
        if (nk == "machines")
        {
            var raw = GetDeep("note2", "field10");
            if (ZplUtil.IsEmpty(raw)) raw = GetDeep("Note2", "field10");
            if (!ZplUtil.IsEmpty(raw)) return raw;
            raw = Str(Get(Get(sub, "note2"), "field10"));
            if (!ZplUtil.IsEmpty(raw)) return raw;
        }
        if (nk is "printedat" or "storedate")
            return Pick(GetDataString("printedAt"), GetDataString("storeDate"), GetDataString("StoreDate"));
        if (nk == "piecesheet") return GetDataString("pieceSheet");
        if (nk == "globalpieceno") return GetDataString("globalPieceNo");

        if (_labelData.ValueKind == JsonValueKind.Object)
        {
            if (TryGetIgnoreCase(_labelData, key, out var direct) && Usable(direct))
                return ToStr(direct);
            foreach (var p in _labelData.EnumerateObject())
            {
                if (NormalizeKey(p.Name) == nk && Usable(p.Value))
                    return ToStr(p.Value);
            }
        }

        var cfgEntry = PreferredMapping(key);
        if (cfgEntry is { } noteCfg)
        {
            var nf = Num(Get(noteCfg, "noteField"));
            var sf = (int)Num(Get(noteCfg, "subField"));
            if (nf > 0 && _labelData.ValueKind == JsonValueKind.Object)
            {
                foreach (var name in new[] { $"note{nf}", $"Note{nf}" })
                {
                    if (!_labelData.TryGetProperty(name, out var raw)) continue;
                    var fromNote = ReadNote(raw, sf);
                    if (!ZplUtil.IsEmpty(fromNote)) return fromNote;
                }
            }
        }

        if (_configuration.ValueKind == JsonValueKind.Object)
        {
            if (TryGetIgnoreCase(_configuration, key, out var cfg) && Usable(cfg) &&
                cfg.ValueKind is not JsonValueKind.Object and not JsonValueKind.Array)
                return ToStr(cfg);
        }
        return "";
    }

    private string GetDataString(string key)
    {
        if (_labelData.ValueKind != JsonValueKind.Object) return "";
        return TryGetIgnoreCase(_labelData, key, out var v) ? ToStr(v) : "";
    }

    private string GetDeep(string a, string b)
    {
        if (_labelData.ValueKind != JsonValueKind.Object) return "";
        if (!_labelData.TryGetProperty(a, out var o) && !TryGetIgnoreCase(_labelData, a, out o))
            return "";
        return Str(Get(o, b));
    }

    private static string ReadNote(JsonElement raw, int sf)
    {
        if (raw.ValueKind == JsonValueKind.Object)
        {
            if (sf != 0 && raw.TryGetProperty(sf.ToString(CultureInfo.InvariantCulture), out var v) && Usable(v))
                return ToStr(v);
            if (sf != 0 && raw.TryGetProperty("field" + sf, out v) && Usable(v))
                return ToStr(v);
            if (raw.TryGetProperty("fields", out var fields))
            {
                if (sf != 0 && fields.ValueKind == JsonValueKind.Object)
                {
                    if (fields.TryGetProperty(sf.ToString(CultureInfo.InvariantCulture), out v) && Usable(v))
                        return ToStr(v);
                    if (fields.TryGetProperty("field" + sf, out v) && Usable(v))
                        return ToStr(v);
                }
                if (fields.ValueKind == JsonValueKind.Array && sf > 0 && sf <= fields.GetArrayLength())
                {
                    v = fields[sf - 1];
                    if (Usable(v)) return ToStr(v);
                }
            }
        }
        if (raw.ValueKind == JsonValueKind.Array && sf > 0 && sf <= raw.GetArrayLength())
        {
            var v = raw[sf - 1];
            if (Usable(v)) return ToStr(v);
        }
        if (raw.ValueKind == JsonValueKind.String && Usable(raw)) return ToStr(raw);
        return "";
    }

    private bool TryUsableLabelCfg(string fieldKey, out JsonElement mapping)
    {
        mapping = default;
        var labels = Get(_configuration, "labels");
        if (labels.ValueKind != JsonValueKind.Object) return false;
        if (!TryGetIgnoreCase(labels, fieldKey, out mapping)) return false;
        return IsUsableMapping(mapping);
    }

    private static bool TryMapping(JsonElement template, string prop, string fieldKey, out JsonElement mapping)
    {
        mapping = default;
        var maps = Get(template, prop);
        if (maps.ValueKind != JsonValueKind.Object) return false;
        if (TryGetIgnoreCase(maps, fieldKey, out mapping) && mapping.ValueKind == JsonValueKind.Object)
            return true;
        var nk = NormalizeKey(fieldKey);
        foreach (var p in maps.EnumerateObject())
        {
            if (NormalizeKey(p.Name) != nk) continue;
            mapping = p.Value;
            return mapping.ValueKind == JsonValueKind.Object;
        }
        return false;
    }

    private static bool IsUsableMapping(JsonElement cfg)
    {
        if (cfg.ValueKind != JsonValueKind.Object) return false;
        if (Get(cfg, "isBlackBox").ValueKind == JsonValueKind.True) return true;
        return Num(Get(cfg, "noteField")) > 0;
    }

    private static JsonElement FirstObject(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in el.EnumerateArray())
                if (x.ValueKind == JsonValueKind.Object) return x;
        }
        if (el.ValueKind == JsonValueKind.Object) return el;
        return default;
    }

    private static string FormatBatch(JsonElement v)
    {
        if (v.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (var x in v.EnumerateArray())
            {
                var s = ToStr(x).Trim();
                if (!ZplUtil.IsEmpty(s)) parts.Add(s);
            }
            return string.Join(", ", parts);
        }
        if (v.ValueKind == JsonValueKind.Object) return "";
        var t = ToStr(v).Trim();
        return ZplUtil.IsEmpty(t) ? "" : t;
    }

    private static bool Usable(JsonElement v)
    {
        if (v.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.Object)
            return false;
        return !ZplUtil.IsEmpty(ToStr(v));
    }

    private static string NormalizeKey(string raw) =>
        Regex.Replace((raw ?? "").Trim().ToLowerInvariant(), @"[\s_-]+", "");

    private static string ToInt(string v)
    {
        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n))
            return Math.Round(n).ToString(CultureInfo.InvariantCulture);
        return v?.Trim() ?? "";
    }

    internal static JsonElement Get(JsonElement obj, string name)
    {
        if (obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v))
            return v;
        if (obj.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in obj.EnumerateObject())
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                    return p.Value;
        }
        return default;
    }

    internal static bool TryGetIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        value = Get(obj, name);
        return value.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null;
    }

    internal static string Str(JsonElement el, string fallback = "")
    {
        if (el.ValueKind == JsonValueKind.String) return el.GetString() ?? fallback;
        if (el.ValueKind == JsonValueKind.Number) return el.ToString();
        if (el.ValueKind is JsonValueKind.True or JsonValueKind.False) return el.ToString();
        return fallback;
    }

    internal static double Num(JsonElement el, double fallback = 0)
    {
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var n)) return n;
        if (el.ValueKind == JsonValueKind.String &&
            double.TryParse(el.GetString()?.Replace("px", "", StringComparison.OrdinalIgnoreCase),
                NumberStyles.Float, CultureInfo.InvariantCulture, out n))
            return n;
        return fallback;
    }

    internal static string ToStr(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (var x in el.EnumerateArray())
            {
                var s = Str(x).Trim();
                if (!ZplUtil.IsEmpty(s)) parts.Add(s);
            }
            return string.Join(", ", parts);
        }
        return Str(el);
    }

    internal static List<string> StringList(JsonElement field, string name)
    {
        var el = Get(field, name);
        var list = new List<string>();
        if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in el.EnumerateArray())
                if (x.ValueKind == JsonValueKind.String) list.Add(x.GetString() ?? "");
        }
        else if (el.ValueKind == JsonValueKind.String)
            list.Add(el.GetString() ?? "");
        return list;
    }
}
