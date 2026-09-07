using Microsoft.AspNetCore.Mvc;
using Spil.LabelPrint.Service.Compilation;
using Spil.LabelPrint.Service.Models;

namespace Spil.LabelPrint.Service.Controllers;

[ApiController]
[Route("api/labels")]
public sealed class LabelsController : ControllerBase
{
    private readonly LabelCompileService _compile;
    private readonly PrinterTcpSender _tcp;
    private readonly IConfiguration _config;

    public LabelsController(LabelCompileService compile, PrinterTcpSender tcp, IConfiguration config)
    {
        _compile = compile;
        _tcp = tcp;
        _config = config;
    }

    /// <summary>Compile one label. Returns printer code in zpl and payload (ERP and Opti).</summary>
    [HttpPost("compile")]
    public ActionResult<CompileLabelResponse> Compile([FromBody] CompileLabelRequest request)
    {
        try
        {
            return Ok(_compile.Compile(request));
        }
        catch (Exception ex)
        {
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    /// <summary>Compile many labels into one concatenated job.</summary>
    [HttpPost("compile-batch")]
    public ActionResult<CompileBatchResponse> CompileBatch([FromBody] CompileBatchRequest request)
    {
        try
        {
            return Ok(_compile.CompileBatch(request));
        }
        catch (Exception ex)
        {
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    /// <summary>Optional: compile (or take ready payload) and send raw to a printer on TCP 9100.</summary>
    [HttpPost("send")]
    public ActionResult<SendPrintResponse> Send([FromBody] SendPrintRequest request)
    {
        if (!_config.GetValue("LabelPrint:AllowTcpSend", true))
            return StatusCode(403, new SendPrintResponse { Ok = false, Message = "TCP send is disabled on this host." });

        try
        {
            var zpl = FirstNonEmpty(request.Zpl, request.Payload);
            if (string.IsNullOrWhiteSpace(zpl) && request.Compile != null)
            {
                var compiled = _compile.Compile(request.Compile);
                zpl = compiled.Payload;
            }
            if (string.IsNullOrWhiteSpace(zpl))
                return BadRequest(new SendPrintResponse { Ok = false, Message = "Provide payload/zpl or compile." });

            _tcp.Send(request.Host, request.Port <= 0 ? 9100 : request.Port, zpl);
            return Ok(new SendPrintResponse { Ok = true, Message = "sent", Zpl = zpl, Payload = zpl });
        }
        catch (Exception ex)
        {
            return BadRequest(new SendPrintResponse { Ok = false, Message = ex.Message });
        }
    }

    static string? FirstNonEmpty(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) ? a : b;
}
