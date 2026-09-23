using Microsoft.AspNetCore.Mvc;
using Spil.LabelPrint.Service.Design;

namespace Spil.LabelPrint.Service.Controllers;

[ApiController]
[Route("api")]
public sealed class DesignController : ControllerBase
{
    private readonly DesignStore _store;

    public DesignController(DesignStore store) => _store = store;

    [HttpGet("field-catalog")]
    public IActionResult Catalog([FromQuery] string? client) =>
        Ok(new { ok = true, client = FieldCatalogs.NormalizeClient(client), fields = FieldCatalogs.For(client ?? "opti") });

    [HttpPost("design-sessions")]
    public ActionResult<DesignSessionResponse> CreateSession([FromBody] DesignSessionRequest request)
    {
        var session = _store.CreateSession(request ?? new DesignSessionRequest());
        return Ok(session);
    }

    [HttpGet("design-sessions/{id}")]
    public ActionResult<DesignSessionResponse> GetSession(string id)
    {
        if (!_store.TryGetSession(id, out var session))
            return NotFound(new { ok = false, error = "Design session expired or was not found." });
        return Ok(session);
    }

    [HttpGet("templates")]
    public IActionResult List([FromQuery] string? client)
    {
        var c = FieldCatalogs.NormalizeClient(client);
        var items = _store.List(c).Select(t => new
        {
            t.Id,
            t.Client,
            t.Name,
            t.LabelType,
            t.SchemaVersion,
            t.Revision,
            t.UpdatedAt,
        });
        return Ok(new { ok = true, client = c, templates = items });
    }

    [HttpGet("templates/{client}/{id}")]
    public IActionResult Get(string client, string id)
    {
        if (!_store.TryGet(client, id, out var record))
            return NotFound(new { ok = false, error = "Template not found." });
        return Ok(record);
    }

    [HttpPost("templates")]
    public IActionResult Create([FromBody] SaveTemplateRequest request)
    {
        try
        {
            return Ok(_store.Save(request ?? new SaveTemplateRequest()));
        }
        catch (Exception ex)
        {
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    [HttpPut("templates/{client}/{id}")]
    public IActionResult Update(string client, string id, [FromBody] SaveTemplateRequest request)
    {
        request ??= new SaveTemplateRequest();
        request.Client = client;
        request.Id = id;
        try
        {
            return Ok(_store.Save(request));
        }
        catch (Exception ex)
        {
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }
}
