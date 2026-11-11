namespace Maf.Client.Identity;

public sealed record AccountRoleChoice(string Name, string Title);

public sealed record AccountUserResponse(string Id, string Email, string[] Roles, bool IsLocked);

public sealed record SaveAccountRequest(string Email, string Password, string[] Roles);

public sealed record UpdateAccountRequest(string[] Roles, bool IsLocked, string? Password);
