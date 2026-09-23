using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Spil.LabelPrint.Service.Design;

public sealed class DesignStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static readonly Regex IdPattern = new("^[A-Za-z0-9._-]{1,80}$", RegexOptions.Compiled);

    private readonly string _root;
    private readonly TimeSpan _sessionTtl;
    private readonly ConcurrentDictionary<string, DesignSessionResponse> _sessions = new();
    private readonly object _fileLock = new();

    public DesignStore(IConfiguration config, IWebHostEnvironment env)
    {
        var configured = config["Designer:TemplateRoot"];
        _root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "designer-templates")
            : configured;
        Directory.CreateDirectory(Path.Combine(_root, "opti"));
        Directory.CreateDirectory(Path.Combine(_root, "erp"));
        var minutes = config.GetValue("Designer:SessionMinutes", 120);
        _sessionTtl = TimeSpan.FromMinutes(Math.Clamp(minutes, 5, 24 * 60));
    }

    public DesignSessionResponse CreateSession(DesignSessionRequest req)
    {
        PurgeExpired();
        var client = FieldCatalogs.NormalizeClient(req.Client);
        var id = Guid.NewGuid().ToString("N");
        JsonElement? template = null;
        if (!string.IsNullOrWhiteSpace(req.TemplateId) && TryGet(client, req.TemplateId!, out var existing))
            template = existing.Template;

        var session = new DesignSessionResponse
        {
            SessionId = id,
            Client = client,
            TemplateId = string.IsNullOrWhiteSpace(req.TemplateId) ? null : req.TemplateId.Trim(),
            LabelType = string.IsNullOrWhiteSpace(req.LabelType) ? "production" : req.LabelType.Trim(),
            ReturnApp = string.IsNullOrWhiteSpace(req.ReturnApp) ? null : req.ReturnApp.Trim(),
            ExpiresAt = DateTimeOffset.UtcNow.Add(_sessionTtl),
            FieldCatalog = req.FieldCatalog is { Count: > 0 } ? req.FieldCatalog : FieldCatalogs.For(client).ToList(),
            PreviewData = req.PreviewData is { ValueKind: JsonValueKind.Object } ? req.PreviewData : null,
            Template = template,
        };
        _sessions[id] = session;
        return session;
    }

    public bool TryGetSession(string sessionId, out DesignSessionResponse session)
    {
        PurgeExpired();
        if (_sessions.TryGetValue(sessionId, out session!))
        {
            if (session.ExpiresAt > DateTimeOffset.UtcNow) return true;
            _sessions.TryRemove(sessionId, out _);
        }
        session = null!;
        return false;
    }

    public IReadOnlyList<TemplateRecord> List(string? client)
    {
        var c = FieldCatalogs.NormalizeClient(client);
        var dir = Path.Combine(_root, c);
        if (!Directory.Exists(dir)) return Array.Empty<TemplateRecord>();
        var list = new List<TemplateRecord>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            if (TryRead(file, out var rec)) list.Add(rec);
        }
        return list.OrderByDescending(t => t.UpdatedAt).ToList();
    }

    public bool TryGet(string client, string id, out TemplateRecord record)
    {
        record = null!;
        if (!IsSafeId(id)) return false;
        var path = PathFor(FieldCatalogs.NormalizeClient(client), id);
        return File.Exists(path) && TryRead(path, out record);
    }

    public TemplateRecord Save(SaveTemplateRequest req)
    {
        var client = FieldCatalogs.NormalizeClient(req.Client);
        if (!string.IsNullOrWhiteSpace(req.SessionId))
        {
            if (!TryGetSession(req.SessionId, out var session))
                throw new InvalidOperationException("Design session expired or was not found.");
            if (!string.Equals(session.Client, client, StringComparison.Ordinal))
                throw new InvalidOperationException("Session client does not match the template client.");
        }

        if (req.Template.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("template must be a JSON object.");

        var id = string.IsNullOrWhiteSpace(req.Id) ? NextId(client) : req.Id.Trim();
        if (!IsSafeId(id))
            throw new ArgumentException("Template id may contain only letters, numbers, dot, dash, and underscore.");

        string? name = req.Name;
        string? labelType = req.LabelType;
        if (req.Template.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
            name ??= n.GetString();
        if (req.Template.TryGetProperty("labelType", out var lt) && lt.ValueKind == JsonValueKind.String)
            labelType ??= lt.GetString();

        var revision = 1;
        if (TryGet(client, id, out var previous)) revision = previous.Revision + 1;

        var record = new TemplateRecord
        {
            Id = id,
            Client = client,
            Name = string.IsNullOrWhiteSpace(name) ? id : name.Trim(),
            LabelType = string.IsNullOrWhiteSpace(labelType) ? "production" : labelType.Trim(),
            SchemaVersion = 1,
            Revision = revision,
            UpdatedAt = DateTimeOffset.UtcNow,
            Template = StampTemplate(req.Template, id, client),
        };

        var path = PathFor(client, id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(record, JsonOpts);
        lock (_fileLock)
        {
            File.WriteAllText(path, json);
        }
        return record;
    }

    private static JsonElement StampTemplate(JsonElement template, string id, string client)
    {
        var node = JsonNode.Parse(template.GetRawText()) as JsonObject ?? new JsonObject();
        node.Remove("previewData");
        node.Remove("labelData");
        node.Remove("fieldCatalog");
        node["id"] = id;
        node["client"] = client;
        return JsonSerializer.SerializeToElement(node);
    }

    private string NextId(string client)
    {
        var n = List(client).Count + 1;
        string id;
        do
        {
            id = $"LBL_{n:000}";
            n++;
        } while (File.Exists(PathFor(client, id)));
        return id;
    }

    private string PathFor(string client, string id) =>
        Path.Combine(_root, client, id + ".json");

    private static bool IsSafeId(string id) => IdPattern.IsMatch(id);

    private static bool TryRead(string path, out TemplateRecord record)
    {
        record = null!;
        try
        {
            var parsed = JsonSerializer.Deserialize<TemplateRecord>(File.ReadAllText(path), JsonOpts);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.Id)) return false;
            record = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void PurgeExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _sessions)
        {
            if (pair.Value.ExpiresAt <= now)
                _sessions.TryRemove(pair.Key, out _);
        }
    }
}
