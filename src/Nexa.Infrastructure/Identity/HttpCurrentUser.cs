using Microsoft.AspNetCore.Http;
using Nexa.Application.Abstractions;
using System.Security.Claims;

namespace Nexa.Infrastructure.Identity;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid UserId
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public string? Email => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email)
        ?? accessor.HttpContext?.User.Identity?.Name;

    public IReadOnlyCollection<string> Roles =>
        accessor.HttpContext?.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];
}
