namespace Maf.Agent.Api.Models.Domain;

[Flags]
public enum ModelCapability
{
    None = 0,
    LargeLanguage = 1,
    Vision = 2,
    ImageGeneration = 4,
    ToolCalling = 8,
    Reasoning = 16,
    AudioInput = 32,
    AudioOutput = 64,
    TextEmbedding = 128,
}

public enum ModelProviderKind
{
    Ollama,
    OpenAI,
    OpenAICompatible,
}

public enum ModelProtocol
{
    Responses,
    ChatCompletions,
}

public sealed record DiscoveredModel
{
    public const int DefaultMaxTokens = 4096;

    public DiscoveredModel(
        string name,
        DateTimeOffset? lastChangedAt,
        int? maxTokens,
        ModelCapability capabilities)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            throw new ArgumentException("نام مدل باید بین ۱ تا ۲۰۰ نویسه باشد.", nameof(name));
        }

        Name = name.Trim();
        LastChangedAt = lastChangedAt?.ToUniversalTime();
        MaxTokens = maxTokens is > 0 and <= 10_000_000
            ? maxTokens.Value
            : DefaultMaxTokens;
        Capabilities = capabilities;
    }

    public string Name { get; }

    public DateTimeOffset? LastChangedAt { get; }

    public int MaxTokens { get; }

    public ModelCapability Capabilities { get; }
}

public sealed class ModelProvider
{
    private readonly List<ProviderModel> _models = [];

    private ModelProvider()
    {
    }

    private ModelProvider(
        Guid id,
        string name,
        ModelProviderKind kind,
        Uri baseAddress,
        string? apiKey,
        ModelProtocol protocol,
        DateTimeOffset createdAt)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Name = name;
        NormalizedName = NormalizeName(name);
        Kind = kind;
        BaseAddress = baseAddress;
        ApiKey = apiKey;
        Protocol = protocol;
        CreatedAt = createdAt.ToUniversalTime();
        UpdatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string NormalizedName { get; private set; } = null!;

    public ModelProviderKind Kind { get; private set; }

    public Uri BaseAddress { get; private set; } = null!;

    public string? ApiKey { get; private set; }

    public ModelProtocol Protocol { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? LastDiscoveredAt { get; private set; }

    public string? DiscoveryError { get; private set; }

    public IReadOnlyCollection<ProviderModel> Models => _models;

    public bool HasApiKey => !string.IsNullOrEmpty(ApiKey);

    public static ModelProvider Register(
        Guid id,
        string name,
        ModelProviderKind kind,
        Uri baseAddress,
        string? apiKey,
        ModelProtocol protocol,
        DateTimeOffset createdAt) =>
        new(
            id,
            RequireName(name),
            RequireKind(kind),
            RequireBaseAddress(baseAddress),
            NormalizeApiKey(apiKey),
            RequireProtocol(protocol),
            createdAt);

    public void Update(
        string name,
        ModelProviderKind kind,
        Uri baseAddress,
        string? apiKey,
        bool replaceApiKey,
        ModelProtocol protocol,
        DateTimeOffset updatedAt)
    {
        name = RequireName(name);
        kind = RequireKind(kind);
        baseAddress = RequireBaseAddress(baseAddress);
        protocol = RequireProtocol(protocol);
        string? nextKey = replaceApiKey ? NormalizeApiKey(apiKey) : ApiKey;

        bool connectionChanged =
            Kind != kind ||
            BaseAddress != baseAddress ||
            Protocol != protocol ||
            ApiKey != nextKey;

        Name = name;
        NormalizedName = NormalizeName(name);
        Kind = kind;
        BaseAddress = baseAddress;
        ApiKey = nextKey;
        Protocol = protocol;
        UpdatedAt = updatedAt.ToUniversalTime();

        if (connectionChanged)
        {
            _models.Clear();
            LastDiscoveredAt = null;
            DiscoveryError = null;
        }
    }

    public void RecordDiscovery(
        IReadOnlyList<DiscoveredModel> models,
        DateTimeOffset discoveredAt)
    {
        ArgumentNullException.ThrowIfNull(models);

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (DiscoveredModel model in models)
        {
            if (!names.Add(model.Name))
            {
                throw new ArgumentException($"ارائه‌دهنده مدل تکراری «{model.Name}» برگرداند.");
            }
        }

        _models.Clear();
        foreach (DiscoveredModel model in models)
        {
            _models.Add(ProviderModel.Create(Id, model));
        }

        DiscoveryError = null;
        LastDiscoveredAt = discoveredAt.ToUniversalTime();
        UpdatedAt = discoveredAt.ToUniversalTime();
    }

    public void RecordDiscoveryFailure(string error, DateTimeOffset failedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        DiscoveryError = error.Length <= 2000 ? error : error[..2000];
        UpdatedAt = failedAt.ToUniversalTime();
    }

    public static string NormalizeName(string name) => RequireName(name).ToLowerInvariant();

    private static string RequireName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            throw new ArgumentException("نام ارائه‌دهنده باید بین ۱ تا ۲۰۰ نویسه باشد.");
        }

        return name.Trim();
    }

    private static ModelProviderKind RequireKind(ModelProviderKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentException("نوع ارائه‌دهنده پشتیبانی نمی‌شود.");
        }

        return kind;
    }

    private static ModelProtocol RequireProtocol(ModelProtocol protocol)
    {
        if (!Enum.IsDefined(protocol))
        {
            throw new ArgumentException("پروتکل باید Responses API یا Chat Completions باشد.");
        }

        return protocol;
    }

    private static Uri RequireBaseAddress(Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        if (!baseAddress.IsAbsoluteUri || baseAddress.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("نشانی پایه باید یک URL مطلق http یا https باشد.");
        }

        if (!string.IsNullOrEmpty(baseAddress.UserInfo))
        {
            throw new ArgumentException("نشانی پایه نباید نام کاربری یا رمز داشته باشد.");
        }

        return baseAddress;
    }

    private static string? NormalizeApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        string trimmed = apiKey.Trim();
        if (trimmed.Length > 2000 || trimmed.Any(char.IsControl))
        {
            throw new ArgumentException("کلید API نامعتبر است.");
        }

        return trimmed;
    }
}

public sealed class ProviderModel
{
    private ProviderModel()
    {
    }

    private ProviderModel(
        Guid providerId,
        string name,
        DateTimeOffset? lastChangedAt,
        int maxTokens,
        ModelCapability capabilities)
    {
        ProviderId = providerId;
        Name = name;
        LastChangedAt = lastChangedAt?.ToUniversalTime();
        MaxTokens = maxTokens;
        Capabilities = capabilities;
    }

    public long Id { get; private set; }

    public Guid ProviderId { get; private set; }

    public string Name { get; private set; } = null!;

    public DateTimeOffset? LastChangedAt { get; private set; }

    public int MaxTokens { get; private set; }

    public ModelCapability Capabilities { get; private set; }

    internal static ProviderModel Create(Guid providerId, DiscoveredModel source) =>
        new(providerId, source.Name, source.LastChangedAt, source.MaxTokens, source.Capabilities);
}
