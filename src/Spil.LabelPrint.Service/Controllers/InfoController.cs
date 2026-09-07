using Microsoft.AspNetCore.Mvc;
using Spil.LabelPrint.Service.Compilation;

namespace Spil.LabelPrint.Service.Controllers;

[ApiController]
public sealed class InfoController : ControllerBase
{
    [HttpGet("api/health")]
    public IActionResult Health() => Ok(new
    {
        ok = true,
        service = "Spil.LabelPrint",
        clients = new[] { "opti", "erp" },
        languages = ZplUtil.Languages,
        brands = ZplUtil.Brands,
        printers = ZplUtil.PrinterCatalog,
        docs = "/swagger",
    });

    /// <summary>Brand → language map for ERP printer dropdowns.</summary>
    [HttpGet("api/brands")]
    public IActionResult Brands() => Ok(new
    {
        ok = true,
        brands = ZplUtil.PrinterCatalog,
    });
}
