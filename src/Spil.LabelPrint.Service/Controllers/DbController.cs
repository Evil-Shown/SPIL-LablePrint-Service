using Microsoft.AspNetCore.Mvc;
using Spil.LabelPrint.Service.Design;

namespace Spil.LabelPrint.Service.Controllers;

[ApiController]
[Route("api/db")]
public sealed class DbController : ControllerBase
{
    private readonly SqlSchemaInspector _inspect;

    public DbController(SqlSchemaInspector inspect) => _inspect = inspect;

    /// <summary>Test SQL Server and list tables/columns for the ERP field palette. Does not store the password.</summary>
    [HttpPost("inspect")]
    public async Task<IActionResult> Inspect([FromBody] DbInspectRequest? request, CancellationToken ct)
    {
        try
        {
            return Ok(await _inspect.InspectAsync(request ?? new DbInspectRequest(), ct));
        }
        catch (Exception ex)
        {
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }
}
