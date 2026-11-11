using Maf.Agent.Api.Identity.Application;

namespace Maf.Agent.Api.Identity.Endpoints;

public static class AccountEndpoints
{
    public static IServiceCollection AddAccountApi(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(
                AccountPolicies.Manage,
                policy => policy.RequireRole(AccountRoles.Administrator));
        return services;
    }

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization(AccountPolicies.Manage);

        group.MapGet("/roles", () => Results.Ok(AccountAccess.Roles));
        group.MapGet("/", ListAsync);
        group.MapGet("/{userId}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{userId}", UpdateAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        AccountRegistry registry,
        CancellationToken cancellationToken) =>
        Results.Ok((await registry.ListAsync(cancellationToken)).Select(ToResponse).ToArray());

    private static async Task<IResult> GetAsync(
        string userId,
        AccountRegistry registry,
        CancellationToken cancellationToken)
    {
        AccountUser? user = await registry.FindAsync(userId, cancellationToken);
        return user is null ? Results.NotFound() : Results.Ok(ToResponse(user));
    }

    private static Task<IResult> CreateAsync(
        SaveAccountRequest request,
        AccountRegistry registry,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            AccountUser user = await registry.RegisterAsync(
                request.Email,
                request.Password,
                request.Roles ?? [],
                cancellationToken);
            return Results.Created($"/api/users/{user.Id}", ToResponse(user));
        });

    private static Task<IResult> UpdateAsync(
        string userId,
        UpdateAccountRequest request,
        AccountRegistry registry,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            AccountUser? user = await registry.UpdateAsync(
                userId,
                request.Roles ?? [],
                request.IsLocked,
                request.Password,
                cancellationToken);
            return user is null ? Results.NotFound() : Results.Ok(ToResponse(user));
        });

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
    }

    private static AccountUserResponse ToResponse(AccountUser user) =>
        new(user.Id, user.Email, user.Roles.ToArray(), user.IsLocked);
}

public sealed record SaveAccountRequest(string Email, string Password, string[]? Roles);

public sealed record UpdateAccountRequest(string[]? Roles, bool IsLocked, string? Password);

public sealed record AccountUserResponse(string Id, string Email, string[] Roles, bool IsLocked);
