using System.Text.Json;

namespace Spil.LabelPrint.Service.Models;

public sealed class CompileLabelRequest
{
    /// <summary>
    /// zebra | honeywell | citizen | sato | sato-sbpl | tsc | godex | datamax | epl.
    /// Service generates ZPL, TSPL, EZPL, SBPL, DPL, or EPL for that brand.
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>Alias for brand (Opti config uses printerBrand).</summary>
    public string? PrinterBrand { get; set; }

    /// <summary>opti | erp. Independent clients; each sends its own template or metro fields.</summary>
    public string? Client { get; set; }

    /// <summary>template = JSON in this request (Opti or ERP file). metro = built-in ERP glass layout only.</summary>
    public string Layout { get; set; } = "template";

    public int? Dpmm { get; set; }
    public int? PrinterDpi { get; set; }
    public double? WidthMm { get; set; }
    public double? HeightMm { get; set; }

    /// <summary>Any Labels-editor JSON (width/height/sections/fieldMappings). Opti and ERP each send their own file — do not mix.</summary>
    public JsonElement? Template { get; set; }

    /// <summary>Piece / order bag for this client's template. Keys match that template's {{tokens}} / fieldKey / fieldMappings.</summary>
    public JsonElement? LabelData { get; set; }

    public JsonElement? Configuration { get; set; }

    /// <summary>Optional Opti project object (subProjects/sheets) for the same aliases Opti uses.</summary>
    public JsonElement? Project { get; set; }

    /// <summary>Used when layout=metro.</summary>
    public MetroLabelFields? Fields { get; set; }
}

public sealed class CompileBatchRequest
{
    public string? Brand { get; set; }
    public string? PrinterBrand { get; set; }
    public string? Client { get; set; }
    public string Layout { get; set; } = "template";
    public int? Dpmm { get; set; }
    public int? PrinterDpi { get; set; }
    public double? WidthMm { get; set; }
    public double? HeightMm { get; set; }
    public JsonElement? Template { get; set; }
    public JsonElement? Configuration { get; set; }
    public JsonElement? Project { get; set; }
    /// <summary>Template layout: piece objects. Metro layout: also accepted as metro field bags.</summary>
    public List<JsonElement> Labels { get; set; } = new();
    public List<MetroLabelFields>? MetroLabels { get; set; }
}

public sealed class MetroLabelFields
{
    public string? OrderNo { get; set; }
    public string? CustOrderNo { get; set; }
    public string? JobDescription { get; set; }
    public string? Dimensions { get; set; }
    public string? GlassSpec { get; set; }
    public string? MarkAs { get; set; }
    public string? DeliveryDate { get; set; }
    public string? Sqm { get; set; }
    public string? LineRef { get; set; }
    public string? Route { get; set; }
    public string? WeightKg { get; set; }
    public List<string>? ProcessNotes { get; set; }
    public string? BarcodeValue { get; set; }
    public string? LogoStoredName { get; set; }
}

public sealed class CompileLabelResponse
{
    public bool Ok { get; set; } = true;
    public string Language { get; set; } = "zpl";
    public string Brand { get; set; } = "zebra";
    public string Client { get; set; } = "";
    public string Layout { get; set; } = "template";
    /// <summary>Printer job (ZPL / TSPL / EZPL / SBPL / DPL). Same as payload.</summary>
    public string Zpl { get; set; } = "";
    /// <summary>Same bytes as zpl. Prefer this name in new ERP clients.</summary>
    public string Payload { get; set; } = "";
    public int WidthDots { get; set; }
    public int HeightDots { get; set; }
    public int Dpmm { get; set; }
}

public sealed class CompileBatchResponse
{
    public bool Ok { get; set; } = true;
    public string Language { get; set; } = "zpl";
    public string Brand { get; set; } = "zebra";
    public string Client { get; set; } = "";
    public string Layout { get; set; } = "template";
    public string Zpl { get; set; } = "";
    public string Payload { get; set; } = "";
    public int Count { get; set; }
}

public sealed class SendPrintRequest
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 9100;
    public string? Zpl { get; set; }
    /// <summary>Alias for zpl (TSPL/EZPL/etc. jobs).</summary>
    public string? Payload { get; set; }
    public CompileLabelRequest? Compile { get; set; }
}

public sealed class SendPrintResponse
{
    public bool Ok { get; set; }
    public string Message { get; set; } = "";
    public string? Zpl { get; set; }
    public string? Payload { get; set; }
}
