using Maf.Agent.Api.Models.Application;
using Maf.Agent.Api.Models.Domain;

namespace Maf.Agent.Api.Models.Endpoints;

public static class ModelEndpoints
{
    public static IServiceCollection AddModelApi(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(
                ModelPolicies.Manage,
                policy => policy.RequireRole(ModelRoles.Manager));
        return services;
    }

    public static IEndpointRouteBuilder MapModelEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/model-providers")
            .WithTags("ModelProviders")
            .RequireAuthorization(ModelPolicies.Manage);

        group.MapGet("/", ListAsync);
        group.MapGet("/defaults", GetDefaultsAsync);
        group.MapPut("/defaults", SaveDefaultsAsync);
        group.MapGet("/{providerId:guid}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{providerId:guid}", UpdateAsync);
        group.MapDelete("/{providerId:guid}", DeleteAsync);
        group.MapPost("/{providerId:guid}/discover", DiscoverAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        ModelProviderRegistry registry,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ModelProvider> providers = await registry.ListAsync(cancellationToken);
        return Results.Ok(providers.Select(ToSummary).ToArray());
    }

    private static async Task<IResult> GetDefaultsAsync(
        ModelDefaultRegistry registry,
        CancellationToken cancellationToken) =>
        Results.Ok(ToDefaults(await registry.ListAsync(cancellationToken)));

    private static Task<IResult> SaveDefaultsAsync(
        SaveModelDefaultsRequest request,
        ModelDefaultRegistry registry,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            IReadOnlyList<ModelDefaultSelection> saved = await registry.SaveAsync(
                (request.Operations ?? []).Select(ToDefaultCommand).ToArray(),
                cancellationToken);
            return Results.Ok(ToDefaults(saved));
        });

    private static async Task<IResult> GetAsync(
        Guid providerId,
        ModelProviderRegistry registry,
        CancellationToken cancellationToken)
    {
        ModelProvider? provider = await registry.FindAsync(providerId, cancellationToken);
        return provider is null ? Results.NotFound() : Results.Ok(ToDetails(provider));
    }

    private static Task<IResult> CreateAsync(
        SaveModelProviderRequest request,
        ModelProviderRegistry registry,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            ModelProvider provider = await registry.RegisterAsync(
                ToCommand(request, replaceApiKey: true),
                cancellationToken);
            return Results.Created($"/api/model-providers/{provider.Id}", ToDetails(provider));
        });

    private static Task<IResult> UpdateAsync(
        Guid providerId,
        SaveModelProviderRequest request,
        ModelProviderRegistry registry,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            ModelProvider? provider = await registry.UpdateAsync(
                providerId,
                ToCommand(request, replaceApiKey: !string.IsNullOrWhiteSpace(request.ApiKey)),
                cancellationToken);
            return provider is null ? Results.NotFound() : Results.Ok(ToDetails(provider));
        });

    private static async Task<IResult> DeleteAsync(
        Guid providerId,
        ModelProviderRegistry registry,
        CancellationToken cancellationToken)
    {
        bool deleted = await registry.DeleteAsync(providerId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> DiscoverAsync(
        Guid providerId,
        ModelProviderRegistry registry,
        CancellationToken cancellationToken)
    {
        ModelProvider? provider = await registry.DiscoverAsync(providerId, cancellationToken);
        return provider is null ? Results.NotFound() : Results.Ok(ToDetails(provider));
    }

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (ModelProviderNameConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
    }

    private static SaveModelDefault ToDefaultCommand(SaveModelDefaultRequest request)
    {
        if (!Enum.TryParse(request.Operation, out ModelOperation operation))
        {
            throw new ArgumentException("عملیات مدل شناخته نشد.");
        }

        return new SaveModelDefault(operation, request.ProviderId, request.ModelName);
    }

    private static ModelDefaultsResponse ToDefaults(IReadOnlyList<ModelDefaultSelection> rows) =>
        new(rows.Select(row => new ModelDefaultResponse(
            row.Operation.ToString(),
            row.Selected?.ProviderId,
            row.Selected?.ProviderName,
            row.Selected?.ModelName,
            row.Choices
                .Select(choice => new ModelDefaultChoiceResponse(
                    choice.ProviderId,
                    choice.ProviderName,
                    choice.ModelName))
                .ToArray())).ToArray());

    private static SaveModelProvider ToCommand(SaveModelProviderRequest request, bool replaceApiKey)
    {
        if (!Enum.TryParse(request.Kind, out ModelProviderKind kind))
        {
            throw new ArgumentException("نوع ارائه‌دهنده باید Ollama، OpenAI یا OpenAICompatible باشد.");
        }

        if (!Enum.TryParse(request.Protocol, out ModelProtocol protocol))
        {
            throw new ArgumentException("پروتکل باید Responses یا ChatCompletions باشد.");
        }

        return new SaveModelProvider(
            request.Name,
            kind,
            request.BaseAddress,
            request.ApiKey,
            replaceApiKey,
            protocol);
    }

    private static ModelProviderSummary ToSummary(ModelProvider provider) =>
        new(
            provider.Id,
            provider.Name,
            provider.Kind.ToString(),
            provider.BaseAddress.AbsoluteUri,
            provider.HasApiKey,
            provider.Protocol.ToString(),
            provider.LastDiscoveredAt,
            provider.DiscoveryError,
            provider.Models.Count);

    private static ModelProviderDetails ToDetails(ModelProvider provider) =>
        new(
            provider.Id,
            provider.Name,
            provider.Kind.ToString(),
            provider.BaseAddress.AbsoluteUri,
            provider.HasApiKey,
            provider.Protocol.ToString(),
            provider.LastDiscoveredAt,
            provider.DiscoveryError,
            provider.Models
                .OrderBy(model => model.Name, StringComparer.Ordinal)
                .Select(model => new ProviderModelResponse(
                    model.Name,
                    model.LastChangedAt,
                    model.MaxTokens,
                    CapabilityNames(model.Capabilities)))
                .ToArray());

    private static string[] CapabilityNames(ModelCapability capabilities) =>
        Enum.GetValues<ModelCapability>()
            .Where(capability => capability != ModelCapability.None && capabilities.HasFlag(capability))
            .Select(capability => capability.ToString())
            .ToArray();
}

public sealed record SaveModelProviderRequest(
    string Name,
    string Kind,
    string BaseAddress,
    string? ApiKey,
    string Protocol);

public sealed record ModelProviderSummary(
    Guid Id,
    string Name,
    string Kind,
    string BaseAddress,
    bool HasApiKey,
    string Protocol,
    DateTimeOffset? LastDiscoveredAt,
    string? DiscoveryError,
    int ModelCount);

public sealed record ModelProviderDetails(
    Guid Id,
    string Name,
    string Kind,
    string BaseAddress,
    bool HasApiKey,
    string Protocol,
    DateTimeOffset? LastDiscoveredAt,
    string? DiscoveryError,
    ProviderModelResponse[] Models);

public sealed record ProviderModelResponse(
    string Name,
    DateTimeOffset? LastChangedAt,
    int MaxTokens,
    string[] Capabilities);

public sealed record SaveModelDefaultsRequest(SaveModelDefaultRequest[] Operations);

public sealed record SaveModelDefaultRequest(string Operation, Guid? ProviderId, string? ModelName);

public sealed record ModelDefaultsResponse(ModelDefaultResponse[] Operations);

public sealed record ModelDefaultResponse(
    string Operation,
    Guid? ProviderId,
    string? ProviderName,
    string? ModelName,
    ModelDefaultChoiceResponse[] Choices);

public sealed record ModelDefaultChoiceResponse(Guid ProviderId, string ProviderName, string ModelName);
