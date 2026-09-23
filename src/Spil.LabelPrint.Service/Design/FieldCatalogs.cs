namespace Spil.LabelPrint.Service.Design;

public static class FieldCatalogs
{
    public static string NormalizeClient(string? client)
    {
        var c = (client ?? "").Trim().ToLowerInvariant();
        return c == "erp" ? "erp" : "opti";
    }

    public static IReadOnlyList<FieldCatalogItem> For(string client)
    {
        return NormalizeClient(client) == "erp" ? Erp : Opti;
    }

    public static readonly FieldCatalogItem[] Opti =
    {
        new() { Key = "customerName", Label = "Customer name", Type = "text" },
        new() { Key = "orderNumber", Label = "Order number", Type = "text" },
        new() { Key = "batchNumber", Label = "Batch number", Type = "text" },
        new() { Key = "pieceDescription", Label = "Piece description", Type = "text" },
        new() { Key = "id", Label = "Piece id", Type = "text" },
        new() { Key = "Dimensions", Label = "Dimensions", Type = "text" },
        new() { Key = "width", Label = "Width", Type = "text" },
        new() { Key = "height", Label = "Height", Type = "text" },
        new() { Key = "area", Label = "Area", Type = "text" },
        new() { Key = "weight", Label = "Weight", Type = "text" },
        new() { Key = "services", Label = "Services", Type = "text" },
        new() { Key = "marks", Label = "Marks", Type = "text" },
        new() { Key = "custPO", Label = "Customer PO", Type = "text" },
        new() { Key = "pickupDate", Label = "Pickup date", Type = "text" },
        new() { Key = "transportType", Label = "Transport", Type = "text" },
        new() { Key = "salesID", Label = "Sales id", Type = "text" },
        new() { Key = "Barcode", Label = "Barcode", Type = "barcode" },
        new() { Key = "note2.field10", Label = "Process checklist (note2.field10)", Type = "text", Source = "notes" },
        new() { Key = "note3.field1", Label = "Note 3 field 1", Type = "text", Source = "notes" },
    };

    public static readonly FieldCatalogItem[] Erp =
    {
        new() { Key = "OrderNo", Label = "Order number", Type = "text" },
        new() { Key = "CustOrderNo", Label = "Customer order", Type = "text" },
        new() { Key = "JobDescription", Label = "Job description", Type = "text" },
        new() { Key = "Dimensions", Label = "Dimensions", Type = "text" },
        new() { Key = "GlassSpec", Label = "Glass spec", Type = "text" },
        new() { Key = "MarkAs", Label = "Mark", Type = "text" },
        new() { Key = "DeliveryDate", Label = "Delivery date", Type = "text" },
        new() { Key = "Sqm", Label = "Square metres", Type = "text" },
        new() { Key = "LineRef", Label = "Line ref", Type = "text" },
        new() { Key = "Route", Label = "Route", Type = "text" },
        new() { Key = "WeightKg", Label = "Weight kg", Type = "text" },
        new() { Key = "Barcode", Label = "Barcode", Type = "barcode" },
        new() { Key = "ProcessNotes", Label = "Process notes", Type = "text" },
    };
}
