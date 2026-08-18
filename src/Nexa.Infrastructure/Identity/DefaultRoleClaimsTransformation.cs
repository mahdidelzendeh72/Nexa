using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Nexa.Application.Common;
using Nexa.Infrastructure.Identity;

namespace Nexa.Infrastructure.Identity;

internal sealed class DefaultRoleClaimsTransformation(UserManager<ApplicationUser> users) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return principal;
        }

        if (principal.IsInRole(NexaRoles.User) || principal.IsInRole(NexaRoles.Admin))
        {
            return principal;
        }

        var user = await users.GetUserAsync(principal);
        if (user is null)
        {
            return principal;
        }

        if (!await users.IsInRoleAsync(user, NexaRoles.User))
        {
            await users.AddToRoleAsync(user, NexaRoles.User);
        }

        if (principal.Identity is ClaimsIdentity identity)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, NexaRoles.User));
        }

        return principal;
    }
}
