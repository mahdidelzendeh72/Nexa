using Maf.Agent.Api.Models.Domain;

namespace Maf.Agent.Api.Models.Application;

public sealed record SaveModelProvider(
    string Name,
    ModelProviderKind Kind,
    string BaseAddress,
    string? ApiKey,
    bool ReplaceApiKey,
    ModelProtocol Protocol);

public sealed class ModelProviderNameConflictException(string name)
    : Exception($"ارائه‌دهنده‌ای با نام «{name}» قبلاً ثبت شده است.")
{
    public string Name { get; } = name;
}

public interface IModelProviderStore
{
    Task<IReadOnlyList<ModelProvider>> ListAsync(CancellationToken cancellationToken);

    Task<ModelProvider?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(ModelProvider provider, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task DeleteAsync(ModelProvider provider, CancellationToken cancellationToken);

    Task ThrowIfNameTakenAsync(
        Guid providerId,
        string normalizedName,
        CancellationToken cancellationToken);
}
