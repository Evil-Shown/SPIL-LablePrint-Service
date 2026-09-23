using System.Text.Json;

namespace Spil.LabelPrint.Service.Design;

public sealed class FieldCatalogItem
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Type { get; set; } = "text";
    public string Source { get; set; } = "piece";
}

public sealed class DesignSessionRequest
{
    public string? Client { get; set; }
    public string? TemplateId { get; set; }
    public string? LabelType { get; set; }
    public string? ReturnApp { get; set; }
    public List<FieldCatalogItem>? FieldCatalog { get; set; }
    public JsonElement? PreviewData { get; set; }
    /// <summary>Optional Labels JSON from Opti/ERP so the designer opens the layout being edited.</summary>
    public JsonElement? Template { get; set; }
}

public sealed class DesignSessionResponse
{
    public bool Ok { get; set; } = true;
    public string SessionId { get; set; } = "";
    public string Client { get; set; } = "";
    public string? TemplateId { get; set; }
    public string? LabelType { get; set; }
    public string? ReturnApp { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public List<FieldCatalogItem> FieldCatalog { get; set; } = new();
    public JsonElement? PreviewData { get; set; }
    public JsonElement? Template { get; set; }
}

public sealed class TemplateRecord
{
    public string Id { get; set; } = "";
    public string Client { get; set; } = "";
    public string Name { get; set; } = "";
    public string LabelType { get; set; } = "production";
    public int SchemaVersion { get; set; } = 1;
    public int Revision { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; }
    public JsonElement Template { get; set; }
}

public sealed class SaveTemplateRequest
{
    public string? Id { get; set; }
    public string? Client { get; set; }
    public string? Name { get; set; }
    public string? LabelType { get; set; }
    public string? SessionId { get; set; }
    public JsonElement Template { get; set; }
}
