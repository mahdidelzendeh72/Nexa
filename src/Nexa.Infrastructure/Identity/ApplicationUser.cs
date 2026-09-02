using Microsoft.AspNetCore.Identity;

namespace Nexa.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }
}
