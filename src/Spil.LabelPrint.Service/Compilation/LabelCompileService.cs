using System.Text.Json;
using Spil.LabelPrint.Service.Models;

namespace Spil.LabelPrint.Service.Compilation;

public sealed class LabelCompileService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IConfiguration _config;

    public LabelCompileService(IConfiguration config) => _config = config;

    public CompileLabelResponse Compile(CompileLabelRequest req)
    {
        req ??= new CompileLabelRequest();
        var brandRaw = First(req.Brand, req.PrinterBrand);
        var (brand, language) = ZplUtil.ResolveBrand(brandRaw);
        var client = ZplUtil.NormalizeClient(req.Client);
        var layout = ZplUtil.NormalizeLayout(req.Layout);
        var dpmm = ZplUtil.ParseDpmm(req.Dpmm, req.PrinterDpi);
        var widthMm = req.WidthMm ?? _config.GetValue("LabelPrint:DefaultWidthMm", 100.0);
        var heightMm = req.HeightMm ?? _config.GetValue("LabelPrint:DefaultHeightMm", 150.0);
        var template = req.Template is { } t ? TemplateZplCompiler.UnwrapTemplate(t) : default;
        var hasTemplate = TemplateZplCompiler.HasFields(template);

        string payload;
        if (layout == "metro")
        {
            var fields = req.Fields ?? MetroFromJson(req.LabelData) ?? new MetroLabelFields();
            payload = MetroZplBuilder.Build(fields, dpmm, widthMm, heightMm);
        }
        else
        {
            if (!hasTemplate)
                throw new ArgumentException(
                    "layout \"template\" needs this client's Labels JSON (sections.fields). Opti and ERP each send their own file. For the built-in ERP glass layout use layout \"metro\".");
            payload = TemplateZplCompiler.Compile(template, req.LabelData, req.Configuration, dpmm, req.Project);
            if (template.TryGetProperty("width", out var w) && w.TryGetDouble(out var tw) && tw > 0)
                widthMm = tw;
            if (template.TryGetProperty("height", out var h) && h.TryGetDouble(out var th) && th > 0)
                heightMm = th;
        }

        payload = LabelJobEncoder.Encode(payload, language, widthMm, heightMm, dpmm);

        return new CompileLabelResponse
        {
            Ok = true,
            Language = language,
            Brand = brand,
            Client = client,
            Layout = layout,
            Zpl = payload,
            Payload = payload,
            Dpmm = dpmm,
            WidthDots = (int)Math.Round(widthMm * dpmm),
            HeightDots = (int)Math.Round(heightMm * dpmm),
        };
    }

    public CompileBatchResponse CompileBatch(CompileBatchRequest req)
    {
        req ??= new CompileBatchRequest();
        var brandRaw = First(req.Brand, req.PrinterBrand);
        var (brand, language) = ZplUtil.ResolveBrand(brandRaw);
        var client = ZplUtil.NormalizeClient(req.Client);
        var layout = ZplUtil.NormalizeLayout(req.Layout);
        var parts = new List<string>();

        if (layout == "metro")
        {
            var bags = req.MetroLabels ?? new List<MetroLabelFields>();
            if (bags.Count == 0)
            {
                foreach (var data in req.Labels)
                {
                    var one = MetroFromJson(data);
                    if (one != null) bags.Add(one);
                }
            }
            if (bags.Count == 0)
                throw new ArgumentException("Metro batch needs metroLabels[] or labels[] with orderNo / barcodeValue fields.");

            foreach (var fields in bags)
            {
                var one = Compile(new CompileLabelRequest
                {
                    Brand = brand,
                    Client = client,
                    Layout = "metro",
                    Dpmm = req.Dpmm,
                    PrinterDpi = req.PrinterDpi,
                    WidthMm = req.WidthMm,
                    HeightMm = req.HeightMm,
                    Fields = fields,
                });
                parts.Add(one.Payload);
            }
        }
        else
        {
            if (req.Labels.Count == 0)
                throw new ArgumentException("Template batch needs labels[] (one object per piece).");
            foreach (var data in req.Labels)
            {
                var one = Compile(new CompileLabelRequest
                {
                    Brand = brand,
                    Client = client,
                    Layout = "template",
                    Dpmm = req.Dpmm,
                    PrinterDpi = req.PrinterDpi,
                    Template = req.Template,
                    Configuration = req.Configuration,
                    Project = req.Project,
                    LabelData = data,
                });
                parts.Add(one.Payload);
            }
        }

        var job = string.Concat(parts);
        return new CompileBatchResponse
        {
            Ok = true,
            Language = language,
            Brand = brand,
            Client = client,
            Layout = layout,
            Count = parts.Count,
            Zpl = job,
            Payload = job,
        };
    }

    static string? First(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) ? a : b;

    static MetroLabelFields? MetroFromJson(JsonElement? el)
    {
        if (el is not { ValueKind: JsonValueKind.Object }) return null;
        return JsonSerializer.Deserialize<MetroLabelFields>(el.Value.GetRawText(), JsonOpts);
    }
}
