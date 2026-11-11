using System.Text.Json;

namespace Maf.Agent.Api.Mcp.Domain;

public enum McpTransportKind
{
    HttpStreamable,
    Stdio,
}

public abstract record McpServerTransport
{
    public sealed record HttpStreamable : McpServerTransport
    {
        public HttpStreamable(Uri endpoint, string? headersJson)
        {
            Endpoint = McpServer.RequireEndpoint(endpoint);
            HeadersJson = McpConnectionJson.Headers(headersJson);
        }

        public Uri Endpoint { get; }

        public string HeadersJson { get; }

        public IReadOnlyDictionary<string, string> Headers =>
            McpConnectionJson.ReadObject(HeadersJson);
    }

    public sealed record Stdio : McpServerTransport
    {
        public Stdio(string command, string? argumentsJson, string? environmentJson)
        {
            Command = McpServer.RequireCommand(command);
            ArgumentsJson = McpConnectionJson.Arguments(argumentsJson);
            EnvironmentJson = McpConnectionJson.Environment(environmentJson);
        }

        public string Command { get; }

        public string ArgumentsJson { get; }

        public string EnvironmentJson { get; }

        public IReadOnlyList<string> Arguments =>
            McpConnectionJson.ReadArguments(ArgumentsJson);

        public IReadOnlyDictionary<string, string> Environment =>
            McpConnectionJson.ReadObject(EnvironmentJson);
    }
}

public sealed record DiscoveredMcpTool
{
    public DiscoveredMcpTool(string name, string? title, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 256)
        {
            throw new ArgumentException("نام ابزار MCP باید بین ۱ تا ۲۵۶ نویسه باشد.", nameof(name));
        }

        Name = name.Trim();
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public string Name { get; }

    public string? Title { get; }

    public string? Description { get; }
}

public sealed class McpServer
{
    public const int MinimumTimeoutSeconds = 1;
    public const int MaximumTimeoutSeconds = 300;

    private readonly List<McpDiscoveredTool> _tools = [];

    private readonly List<McpToolApproval> _toolApprovals = [];

    private McpServer()
    {
    }

    private McpServer(
        Guid id,
        string name,
        string? description,
        McpServerTransport transport,
        int timeoutSeconds,
        bool isEnabled,
        DateTimeOffset createdAt)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Name = name;
        NormalizedName = NormalizeName(name);
        Description = description;
        TimeoutSeconds = timeoutSeconds;
        IsEnabled = isEnabled;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        ApplyTransport(transport);
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string NormalizedName { get; private set; } = null!;

    public string? Description { get; private set; }

    public McpTransportKind Transport { get; private set; }

    public Uri? Endpoint { get; private set; }

    public string? HeadersJson { get; private set; }

    public string? Command { get; private set; }

    public string? ArgumentsJson { get; private set; }

    public string? EnvironmentJson { get; private set; }

    public int TimeoutSeconds { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? LastDiscoveredAt { get; private set; }

    public string? DiscoveryError { get; private set; }

    public IReadOnlyCollection<McpDiscoveredTool> Tools => _tools;

    public IReadOnlyCollection<McpToolApproval> ToolApprovals => _toolApprovals;

    public bool RequiresApproval(string name) =>
        _toolApprovals.Any(approval =>
            string.Equals(approval.Name, name, StringComparison.Ordinal) &&
            approval.RequiresApproval);

    public static McpServer Register(
        Guid id,
        string name,
        string? description,
        McpServerTransport transport,
        int timeoutSeconds,
        bool isEnabled,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(transport);
        return new McpServer(
            id,
            RequireName(name),
            NormalizeDescription(description),
            transport,
            RequireTimeout(timeoutSeconds),
            isEnabled,
            createdAt);
    }

    public void Update(
        string name,
        string? description,
        McpServerTransport transport,
        int timeoutSeconds,
        bool isEnabled,
        DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(transport);
        name = RequireName(name);
        timeoutSeconds = RequireTimeout(timeoutSeconds);

        bool connectionChanged = TimeoutSeconds != timeoutSeconds || !SameTransport(transport);
        Name = name;
        NormalizedName = NormalizeName(name);
        Description = NormalizeDescription(description);
        TimeoutSeconds = timeoutSeconds;
        IsEnabled = isEnabled;
        UpdatedAt = updatedAt;
        ApplyTransport(transport);

        if (connectionChanged)
        {
            _tools.Clear();
            LastDiscoveredAt = null;
            DiscoveryError = null;
        }
    }

    public McpServerTransport ToTransport() =>
        Transport switch
        {
            McpTransportKind.HttpStreamable => new McpServerTransport.HttpStreamable(
                Endpoint ?? throw new InvalidOperationException("آدرس سرور HTTP ذخیره نشده است."),
                HeadersJson),
            McpTransportKind.Stdio => new McpServerTransport.Stdio(
                Command ?? throw new InvalidOperationException("دستور Stdio ذخیره نشده است."),
                ArgumentsJson,
                EnvironmentJson),
            _ => throw new InvalidOperationException($"نوع انتقال '{Transport}' پشتیبانی نمی‌شود."),
        };

    public void RecordDiscovery(
        IReadOnlyList<DiscoveredMcpTool> tools,
        DateTimeOffset discoveredAt)
    {
        ArgumentNullException.ThrowIfNull(tools);

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (DiscoveredMcpTool tool in tools)
        {
            if (!names.Add(tool.Name))
            {
                throw new ArgumentException($"سرور MCP ابزار تکراری «{tool.Name}» برگرداند.");
            }
        }

        _tools.Clear();
        foreach (DiscoveredMcpTool tool in tools)
        {
            _tools.Add(McpDiscoveredTool.Create(Id, tool));
        }

        DiscoveryError = null;
        LastDiscoveredAt = discoveredAt;
        UpdatedAt = discoveredAt;
    }

    public void SetToolApprovals(IReadOnlyList<McpToolApprovalSetting> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        HashSet<string> discovered = _tools
            .Select(tool => tool.Name)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (McpToolApprovalSetting setting in settings)
        {
            string name = RequireToolName(setting.Name);
            if (!seen.Add(name))
            {
                throw new ArgumentException($"Tool '{name}' is listed more than once.");
            }

            if (!discovered.Contains(name))
            {
                throw new ArgumentException($"Tool '{name}' is not currently discovered on this server.");
            }

            McpToolApproval? approval = _toolApprovals.FirstOrDefault(item =>
                string.Equals(item.Name, name, StringComparison.Ordinal));
            if (approval is null)
            {
                _toolApprovals.Add(McpToolApproval.Create(Id, name, setting.RequiresApproval));
            }
            else
            {
                approval.SetRequiresApproval(setting.RequiresApproval);
            }
        }
    }

    public void RecordDiscoveryFailure(string error, DateTimeOffset failedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        DiscoveryError = error.Length <= 2000 ? error : error[..2000];
        UpdatedAt = failedAt;
    }

    public static string NormalizeName(string name) => RequireName(name).ToLowerInvariant();

    internal static string RequireCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command) || command.Trim().Length > 260 ||
            HasControlCharacter(command))
        {
            throw new ArgumentException("دستور Stdio باید بین ۱ تا ۲۶۰ نویسه و بدون نویسه کنترلی باشد.");
        }

        return command.Trim();
    }

    internal static Uri RequireEndpoint(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("آدرس سرور باید یک URL مطلق http یا https باشد.");
        }

        if (!string.IsNullOrEmpty(endpoint.UserInfo))
        {
            throw new ArgumentException("آدرس سرور نباید نام کاربری یا رمز داشته باشد.");
        }

        return endpoint;
    }

    private void ApplyTransport(McpServerTransport transport)
    {
        switch (transport)
        {
            case McpServerTransport.HttpStreamable http:
                Transport = McpTransportKind.HttpStreamable;
                Endpoint = http.Endpoint;
                HeadersJson = http.HeadersJson;
                Command = null;
                ArgumentsJson = null;
                EnvironmentJson = null;
                break;
            case McpServerTransport.Stdio stdio:
                Transport = McpTransportKind.Stdio;
                Endpoint = null;
                HeadersJson = null;
                Command = stdio.Command;
                ArgumentsJson = stdio.ArgumentsJson;
                EnvironmentJson = stdio.EnvironmentJson;
                break;
            default:
                throw new ArgumentException("نوع انتقال پشتیبانی نمی‌شود.");
        }
    }

    private bool SameTransport(McpServerTransport transport) =>
        transport switch
        {
            McpServerTransport.HttpStreamable http =>
                Transport == McpTransportKind.HttpStreamable &&
                Endpoint == http.Endpoint &&
                HeadersJson == http.HeadersJson,
            McpServerTransport.Stdio stdio =>
                Transport == McpTransportKind.Stdio &&
                Command == stdio.Command &&
                ArgumentsJson == stdio.ArgumentsJson &&
                EnvironmentJson == stdio.EnvironmentJson,
            _ => false,
        };

    private static string RequireName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            throw new ArgumentException("نام سرور باید بین ۱ تا ۲۰۰ نویسه باشد.");
        }

        return name.Trim();
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        string trimmed = description.Trim();
        if (trimmed.Length > 2000)
        {
            throw new ArgumentException("توضیحات نباید بیشتر از ۲۰۰۰ نویسه باشد.");
        }

        return trimmed;
    }

    private static int RequireTimeout(int timeoutSeconds)
    {
        if (timeoutSeconds is < MinimumTimeoutSeconds or > MaximumTimeoutSeconds)
        {
            throw new ArgumentException("مهلت زمانی باید بین ۱ تا ۳۰۰ ثانیه باشد.");
        }

        return timeoutSeconds;
    }

    private static string RequireToolName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 256)
        {
            throw new ArgumentException("The tool name must be between 1 and 256 characters.");
        }

        return name.Trim();
    }

    private static bool HasControlCharacter(string value) =>
        value.Any(char.IsControl);
}

public sealed record McpToolApprovalSetting(string Name, bool RequiresApproval);

public sealed class McpToolApproval
{
    private McpToolApproval()
    {
    }

    private McpToolApproval(Guid serverId, string name, bool requiresApproval)
    {
        ServerId = serverId;
        Name = name;
        RequiresApproval = requiresApproval;
    }

    public long Id { get; private set; }

    public Guid ServerId { get; private set; }

    public string Name { get; private set; } = null!;

    public bool RequiresApproval { get; private set; }

    internal void SetRequiresApproval(bool requiresApproval) => RequiresApproval = requiresApproval;

    internal static McpToolApproval Create(Guid serverId, string name, bool requiresApproval) =>
        new(serverId, name, requiresApproval);
}

public sealed class McpDiscoveredTool
{
    private McpDiscoveredTool()
    {
    }

    private McpDiscoveredTool(
        Guid serverId,
        string name,
        string? title,
        string? description)
    {
        ServerId = serverId;
        Name = name;
        Title = title;
        Description = description;
    }

    public long Id { get; private set; }

    public Guid ServerId { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Title { get; private set; }

    public string? Description { get; private set; }

    internal static McpDiscoveredTool Create(Guid serverId, DiscoveredMcpTool source)
    {
        string? title = source.Title is { Length: > 512 }
            ? source.Title[..512]
            : source.Title;
        return new McpDiscoveredTool(serverId, source.Name, title, source.Description);
    }
}

internal static class McpConnectionJson
{
    private const int MaximumDocumentLength = 8000;
    private const int MaximumEntries = 32;

    public static string Headers(string? json) =>
        CanonicalObject(
            json,
            "{}",
            "هدرها باید یک شیء JSON از مقدارهای متنی باشند.",
            "نام یا مقدار هدر نامعتبر است.",
            RequireHeader);

    public static string Environment(string? json) =>
        CanonicalObject(
            json,
            "{}",
            "متغیرهای محیطی Stdio باید یک شیء JSON از مقدارهای متنی باشند.",
            "نام یا مقدار متغیر محیطی نامعتبر است.",
            RequireEnvironmentVariable);

    public static string Arguments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "[]";
        }

        using JsonDocument document = Parse(
            json,
            "آرگومان‌های Stdio باید یک آرایه JSON از متن‌ها باشد.");
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("آرگومان‌های Stdio باید یک آرایه JSON از متن‌ها باشد.");
        }

        List<string> values = [];
        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not { } value ||
                string.IsNullOrWhiteSpace(value) || value.Length > 1024 ||
                value.Any(char.IsControl))
            {
                throw new ArgumentException("هر آرگومان Stdio باید یک متن غیرخالی و بدون نویسه کنترلی باشد.");
            }

            values.Add(value);
            if (values.Count > MaximumEntries)
            {
                throw new ArgumentException("تعداد آرگومان‌های Stdio نباید بیشتر از ۳۲ باشد.");
            }
        }

        return JsonSerializer.Serialize(values);
    }

    public static IReadOnlyDictionary<string, string> ReadObject(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(json)
        ?? new Dictionary<string, string>();

    public static IReadOnlyList<string> ReadArguments(string json) =>
        JsonSerializer.Deserialize<string[]>(json) ?? [];

    private static string CanonicalObject(
        string? json,
        string empty,
        string invalidDocument,
        string invalidEntry,
        Action<string, string> validate)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return empty;
        }

        using JsonDocument document = Parse(json, invalidDocument);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException(invalidDocument);
        }

        SortedDictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String ||
                property.Value.GetString() is not { } entry)
            {
                throw new ArgumentException(invalidEntry);
            }

            validate(property.Name, entry);
            if (!values.TryAdd(property.Name, entry))
            {
                throw new ArgumentException(invalidEntry);
            }

            if (values.Count > MaximumEntries)
            {
                throw new ArgumentException(invalidEntry);
            }
        }

        return JsonSerializer.Serialize(values);
    }

    private static JsonDocument Parse(string json, string error)
    {
        if (json.Length > MaximumDocumentLength)
        {
            throw new ArgumentException(error);
        }

        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw new ArgumentException(error);
        }
    }

    private static void RequireHeader(string name, string value)
    {
        if (name.Length is < 1 or > 128 || value.Length > 4096 ||
            name.Any(character => char.IsControl(character) || character is ':' or ' ') ||
            value.Any(char.IsControl))
        {
            throw new ArgumentException("نام یا مقدار هدر نامعتبر است.");
        }
    }

    private static void RequireEnvironmentVariable(string name, string value)
    {
        if (name.Length is < 1 or > 128 || value.Length > 4096 ||
            name.Any(character => char.IsControl(character) || character == '=') ||
            value.Any(char.IsControl))
        {
            throw new ArgumentException("نام یا مقدار متغیر محیطی نامعتبر است.");
        }
    }
}
