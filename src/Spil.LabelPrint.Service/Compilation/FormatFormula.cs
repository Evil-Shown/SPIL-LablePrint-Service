using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Spil.LabelPrint.Service.Compilation;

/// <summary>
/// Same rule language as Label Crafter.
/// if {Field} = "HOLD" then suppress
/// if {OrderNo} &lt;&gt; "" then text "Job " &amp; {OrderNo}
/// </summary>
public static class FormatFormula
{
    public sealed class Result
    {
        public bool Suppress { get; set; }
        public bool? Bold { get; set; }
        public bool? Italic { get; set; }
        public string? Text { get; set; }
        public string? Align { get; set; }
        public double? FontSize { get; set; }
    }

    public static Result Apply(JsonElement field, Func<string, string> lookup, string printedText, HashSet<string> seen)
    {
        var result = new Result();
        if (Bool(field, "suppress") || Bool(field, "hidden")) result.Suppress = true;
        var rules = Str(field, "formatRules");
        if (!string.IsNullOrWhiteSpace(rules))
        {
            foreach (var raw in rules.Split('\n', '\r'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                if (!TrySplit(line, out var cond, out var thenPart, out var elsePart)) continue;
                var ok = IsTruthy(Eval(cond, lookup));
                var action = ok ? thenPart : elsePart;
                if (string.IsNullOrWhiteSpace(action)) continue;
                ApplyAction(action, lookup, result);
            }
        }
        var text = result.Text ?? printedText ?? "";
        if (Bool(field, "suppressIfDuplicated"))
        {
            if (seen.Contains(text)) result.Suppress = true;
            else if (!string.IsNullOrEmpty(text)) seen.Add(text);
        }
        return result;
    }

    private static void ApplyAction(string action, Func<string, string> lookup, Result result)
    {
        var a = action.Trim();
        var key = a.ToLowerInvariant();
        if (key is "bold") { result.Bold = true; return; }
        if (key is "normal" or "regular") { result.Bold = false; result.Italic = false; return; }
        if (key is "italic") { result.Italic = true; return; }
        if (key is "suppress" or "hide") { result.Suppress = true; return; }
        if (key is "show") { result.Suppress = false; return; }
        var size = Regex.Match(a, @"^size\s+([0-9.]+)$", RegexOptions.IgnoreCase);
        if (size.Success && double.TryParse(size.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
        {
            result.FontSize = n;
            return;
        }
        var align = Regex.Match(a, @"^align\s+(left|center|right)$", RegexOptions.IgnoreCase);
        if (align.Success) { result.Align = align.Groups[1].Value.ToLowerInvariant(); return; }
        var text = Regex.Match(a, @"^text\s+(.+)$", RegexOptions.IgnoreCase);
        if (text.Success) { result.Text = AsString(Eval(text.Groups[1].Value, lookup)); return; }
    }

    private static bool TrySplit(string line, out string cond, out string thenPart, out string elsePart)
    {
        cond = thenPart = elsePart = "";
        var lower = line.ToLowerInvariant();
        if (!lower.StartsWith("if ")) return false;
        var thenAt = lower.IndexOf(" then ", StringComparison.Ordinal);
        if (thenAt < 0) return false;
        var elseAt = lower.IndexOf(" else ", thenAt + 6, StringComparison.Ordinal);
        cond = line[3..thenAt].Trim();
        thenPart = (elseAt < 0 ? line[(thenAt + 6)..] : line[(thenAt + 6)..elseAt]).Trim();
        elsePart = elseAt < 0 ? "" : line[(elseAt + 6)..].Trim();
        return cond.Length > 0;
    }

    private static object? Eval(string src, Func<string, string> lookup)
    {
        var tokens = Tokenize(src);
        var i = 0;
        return Or();

        object? Or()
        {
            var v = And();
            while (IsWord("or")) { i++; v = IsTruthy(v) || IsTruthy(And()); }
            return v;
        }
        object? And()
        {
            var v = Cmp();
            while (IsWord("and")) { i++; v = IsTruthy(v) && IsTruthy(Cmp()); }
            return v;
        }
        object? Cmp()
        {
            var left = Value();
            if (i < tokens.Count && tokens[i].Op)
            {
                var op = tokens[i++].Text;
                var right = Value();
                return Compare(left, op, right);
            }
            return left;
        }
        object? Value()
        {
            var parts = new List<object?> { Primary() };
            while (i < tokens.Count && tokens[i].Text == "&") { i++; parts.Add(Primary()); }
            if (parts.Count == 1) return parts[0];
            return string.Concat(parts.Select(p => p?.ToString() ?? ""));
        }
        object? Primary()
        {
            if (i >= tokens.Count) return "";
            var t = tokens[i++];
            if (t.Kind == "str") return t.Text;
            if (t.Kind == "num") return t.Number;
            if (t.Kind == "field") return lookup(t.Text) ?? "";
            if (t.Kind == "id")
            {
                var id = t.Text.ToLowerInvariant();
                if (id == "true") return true;
                if (id == "false") return false;
                if (id == "not") return !IsTruthy(Or());
                return lookup(t.Text) ?? "";
            }
            if (t.Text == "(")
            {
                var v = Or();
                if (i < tokens.Count && tokens[i].Text == ")") i++;
                return v;
            }
            return "";
        }
        bool IsWord(string w) => i < tokens.Count && tokens[i].Kind == "id" && tokens[i].Text.Equals(w, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Compare(object? left, string op, object? right)
    {
        var ls = left?.ToString() ?? "";
        var rs = right?.ToString() ?? "";
        double ln = 0, rn = 0;
        var both = double.TryParse(ls, NumberStyles.Float, CultureInfo.InvariantCulture, out ln)
            && double.TryParse(rs, NumberStyles.Float, CultureInfo.InvariantCulture, out rn)
            && ls.Length > 0 && rs.Length > 0;
        var gt = both ? ln.CompareTo(rn) : string.Compare(ls, rs, StringComparison.OrdinalIgnoreCase);
        return op switch
        {
            "=" or "==" => gt == 0,
            "<>" or "!=" => gt != 0,
            ">" => gt > 0,
            "<" => gt < 0,
            ">=" => gt >= 0,
            "<=" => gt <= 0,
            _ => false,
        };
    }

    private static bool IsTruthy(object? v)
    {
        if (v is bool b) return b;
        var s = (v?.ToString() ?? "").Trim().ToLowerInvariant();
        return s is not ("" or "0" or "false" or "no" or "n");
    }

    private static string AsString(object? v) => v?.ToString() ?? "";

    private sealed class Tok
    {
        public string Kind = "";
        public string Text = "";
        public double Number;
        public bool Op;
    }

    private static List<Tok> Tokenize(string src)
    {
        var list = new List<Tok>();
        var i = 0;
        while (i < src.Length)
        {
            var c = src[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '"')
            {
                var j = i + 1;
                var buf = "";
                while (j < src.Length && src[j] != '"') buf += src[j++];
                list.Add(new Tok { Kind = "str", Text = buf });
                i = j + 1;
                continue;
            }
            if (c == '{')
            {
                var j = src.IndexOf('}', i + 1);
                var end = j < 0 ? src.Length : j;
                list.Add(new Tok { Kind = "field", Text = src[(i + 1)..end].Trim() });
                i = j < 0 ? src.Length : j + 1;
                continue;
            }
            if (c is '&' or '(' or ')')
            {
                list.Add(new Tok { Kind = "sym", Text = c.ToString() });
                i++;
                continue;
            }
            if (i + 1 < src.Length && (src.Substring(i, 2) is "<>" or ">=" or "<=" or "!="))
            {
                list.Add(new Tok { Kind = "op", Text = src.Substring(i, 2), Op = true });
                i += 2;
                continue;
            }
            if (c is '=' or '>' or '<')
            {
                list.Add(new Tok { Kind = "op", Text = c.ToString(), Op = true });
                i++;
                continue;
            }
            if (char.IsDigit(c) || c == '.')
            {
                var j = i + 1;
                while (j < src.Length && (char.IsDigit(src[j]) || src[j] == '.')) j++;
                double.TryParse(src[i..j], NumberStyles.Float, CultureInfo.InvariantCulture, out var n);
                list.Add(new Tok { Kind = "num", Number = n, Text = src[i..j] });
                i = j;
                continue;
            }
            if (char.IsLetter(c) || c == '_')
            {
                var j = i + 1;
                while (j < src.Length && (char.IsLetterOrDigit(src[j]) || src[j] == '_')) j++;
                list.Add(new Tok { Kind = "id", Text = src[i..j] });
                i = j;
                continue;
            }
            i++;
        }
        return list;
    }

    private static string Str(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object) return "";
        if (!el.TryGetProperty(name, out var v)) return "";
        return v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    }

    private static bool Bool(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var v)) return false;
        return v.ValueKind == JsonValueKind.True;
    }
}
