using Maf.Agent.Api.Knowledge.Application;
using Maf.Agent.Api.Mcp.Application;
using Maf.Agent.Api.Models.Application;

namespace Maf.Agent.Api.Identity.Application;

public static class AccountRoles
{
    public const string Administrator = "UserAdministrator";
}

public static class AccountPolicies
{
    public const string Manage = "Accounts.Manage";
}

public sealed record AccountRoleChoice(string Name, string Title);

public static class AccountAccess
{
    public static IReadOnlyList<AccountRoleChoice> Roles { get; } =
    [
        new(KnowledgeRoles.Reader, "خواندن دانش"),
        new(KnowledgeRoles.Manager, "مدیریت دانش"),
        new(McpRoles.Manager, "مدیریت سرورهای MCP"),
        new(ModelRoles.Manager, "مدیریت مدل‌ها"),
        new(AccountRoles.Administrator, "مدیریت کاربران"),
    ];

    public static string[] Normalize(IEnumerable<string>? roles)
    {
        string[] distinct = (roles ?? [])
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (distinct.Any(role => Roles.All(choice => choice.Name != role)))
        {
            throw new ArgumentException("نقش انتخاب‌شده شناخته نیست.");
        }

        return distinct;
    }
}
