using Maf.Agent.Api.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maf.Agent.Api.Identity.Application;

public sealed record AccountUser(string Id, string Email, IReadOnlyList<string> Roles, bool IsLocked);

public sealed class AccountRegistry(UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles)
{
    public async Task<IReadOnlyList<AccountUser>> ListAsync(CancellationToken cancellationToken)
    {
        ApplicationUser[] stored = await users.Users
            .OrderBy(user => user.Email)
            .ToArrayAsync(cancellationToken);
        AccountUser[] described = new AccountUser[stored.Length];
        for (int index = 0; index < stored.Length; index++)
        {
            described[index] = await DescribeAsync(stored[index]);
        }

        return described;
    }

    public async Task<AccountUser?> FindAsync(string userId, CancellationToken cancellationToken)
    {
        ApplicationUser? user = await users.FindByIdAsync(userId);
        return user is null ? null : await DescribeAsync(user);
    }

    public async Task<AccountUser> RegisterAsync(
        string email,
        string password,
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken)
    {
        string address = RequireEmail(email);
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("رمز عبور لازم است.");
        }

        string[] assigned = AccountAccess.Normalize(roleNames);
        await EnsureRolesExistAsync();
        ApplicationUser user = new()
        {
            UserName = address,
            Email = address,
            EmailConfirmed = true,
            LockoutEnabled = true,
        };
        EnsureSucceeded(await users.CreateAsync(user, password));
        if (assigned.Length > 0)
        {
            EnsureSucceeded(await users.AddToRolesAsync(user, assigned));
        }

        return await DescribeAsync(user);
    }

    public async Task<AccountUser?> UpdateAsync(
        string userId,
        IReadOnlyCollection<string> roleNames,
        bool isLocked,
        string? password,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await users.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        string[] assigned = AccountAccess.Normalize(roleNames);
        await EnsureAdministratorRemainsAsync(user, assigned, isLocked);
        await EnsureRolesExistAsync();
        if (!string.IsNullOrWhiteSpace(password))
        {
            if (await users.HasPasswordAsync(user))
            {
                EnsureSucceeded(await users.RemovePasswordAsync(user));
            }

            EnsureSucceeded(await users.AddPasswordAsync(user, password));
        }

        IList<string> current = await users.GetRolesAsync(user);
        string[] remove = current.Except(assigned, StringComparer.Ordinal).ToArray();
        string[] add = assigned.Except(current, StringComparer.Ordinal).ToArray();
        if (remove.Length > 0)
        {
            EnsureSucceeded(await users.RemoveFromRolesAsync(user, remove));
        }

        if (add.Length > 0)
        {
            EnsureSucceeded(await users.AddToRolesAsync(user, add));
        }

        EnsureSucceeded(await users.SetLockoutEnabledAsync(user, true));
        EnsureSucceeded(await users.SetLockoutEndDateAsync(
            user,
            isLocked ? DateTimeOffset.UtcNow.AddYears(100) : null));
        return await DescribeAsync(await users.FindByIdAsync(user.Id) ?? user);
    }

    private async Task EnsureAdministratorRemainsAsync(
        ApplicationUser user,
        IReadOnlyCollection<string> assigned,
        bool isLocked)
    {
        IList<string> current = await users.GetRolesAsync(user);
        if (!current.Contains(AccountRoles.Administrator, StringComparer.Ordinal))
        {
            return;
        }

        bool keepsAdministration = assigned.Contains(AccountRoles.Administrator, StringComparer.Ordinal) && !isLocked;
        if (keepsAdministration)
        {
            return;
        }

        IList<ApplicationUser> administrators = await users.GetUsersInRoleAsync(AccountRoles.Administrator);
        if (administrators.Count <= 1)
        {
            throw new ArgumentException("حداقل یک مدیر کاربران باید فعال بماند.");
        }
    }

    private async Task EnsureRolesExistAsync()
    {
        foreach (AccountRoleChoice choice in AccountAccess.Roles)
        {
            if (await roles.RoleExistsAsync(choice.Name))
            {
                continue;
            }

            EnsureSucceeded(await roles.CreateAsync(new IdentityRole(choice.Name)));
        }
    }

    private async Task<AccountUser> DescribeAsync(ApplicationUser user)
    {
        IList<string> assigned = await users.GetRolesAsync(user);
        return new AccountUser(
            user.Id,
            user.Email ?? user.UserName ?? "",
            assigned.OrderBy(role => role, StringComparer.Ordinal).ToArray(),
            user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow);
    }

    private static string RequireEmail(string email)
    {
        string trimmed = email.Trim();
        if (trimmed.Length is 0 or > 200 || !trimmed.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("ایمیل معتبر نیست.");
        }

        return trimmed;
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        throw new ArgumentException(string.Join(
            " ",
            result.Errors.Select(Describe).Distinct(StringComparer.Ordinal)));
    }

    private static string Describe(IdentityError error) => error.Code switch
    {
        "DuplicateEmail" or "DuplicateUserName" => "این ایمیل قبلاً ثبت شده است.",
        "PasswordTooShort" => "رمز عبور باید حداقل ۶ نویسه باشد.",
        "PasswordRequiresDigit" => "رمز عبور باید عدد داشته باشد.",
        "PasswordRequiresLower" => "رمز عبور باید حرف کوچک انگلیسی داشته باشد.",
        "PasswordRequiresUpper" => "رمز عبور باید حرف بزرگ انگلیسی داشته باشد.",
        "PasswordRequiresNonAlphanumeric" => "رمز عبور باید یک نشانه مثل ! داشته باشد.",
        "InvalidEmail" => "ایمیل معتبر نیست.",
        _ => error.Description,
    };
}
