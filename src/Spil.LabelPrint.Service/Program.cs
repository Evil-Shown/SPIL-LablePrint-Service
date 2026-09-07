using Microsoft.OpenApi.Models;
using Spil.LabelPrint.Service.Compilation;

if (args.Length >= 2 && string.Equals(args[0], "--compile", StringComparison.OrdinalIgnoreCase))
{
    var template = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
        File.ReadAllText(args[1]));
    System.Text.Json.JsonElement? data = args.Length > 2
        ? System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(File.ReadAllText(args[2]))
        : null;
    Console.Write(TemplateZplCompiler.Compile(template, data, null, 12, null));
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SPIL Label Print Service",
        Version = "v1",
        Description = "Compile labels for Opti and ERP as independent clients (each sends its own template, or ERP uses layout metro). Output: ZPL, TSPL, EZPL, SBPL, DPL, or EPL.",
    });
});
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddSingleton<LabelCompileService>();
builder.Services.AddSingleton<PrinterTcpSender>();

var app = builder.Build();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/swagger/v1/swagger.json", "SPIL Label Print Service v1");
});
app.MapControllers();
app.Run();
