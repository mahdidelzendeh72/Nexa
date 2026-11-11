using Maf.Agent.Api.Identity.Application;
using Maf.Agent.Api.Identity.Domain;
using Maf.Agent.Api.Knowledge.Application;
using Maf.Agent.Api.Models.Application;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Maf.Agent.Api.Tests;

public sealed class AccountRegistryTests
{
    [Fact]
    public async Task Register_assigns_only_the_selected_roles()
    {
        await using AccountFixture fixture = await AccountFixture.CreateAsync();

        AccountUser created = await fixture.Registry.RegisterAsync(
            "reader@example.com",
            "Test123!",
            [KnowledgeRoles.Reader, ModelRoles.Manager],
            CancellationToken.None);

        Assert.Equal(["KnowledgeReader", "ModelManager"], created.Roles);
        Assert.False(created.IsLocked);
    }

    [Fact]
    public async Task Unknown_role_is_rejected()
    {
        await using AccountFixture fixture = await AccountFixture.CreateAsync();

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Registry.RegisterAsync(
                "reader@example.com",
                "Test123!",
                ["Owner"],
                CancellationToken.None));

        Assert.Equal("نقش انتخاب‌شده شناخته نیست.", exception.Message);
    }

    [Fact]
    public async Task Duplicate_email_is_rejected()
    {
        await using AccountFixture fixture = await AccountFixture.CreateAsync();
        await fixture.Registry.RegisterAsync("reader@example.com", "Test123!", [], CancellationToken.None);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Registry.RegisterAsync("reader@example.com", "Test123!", [], CancellationToken.None));

        Assert.Equal("این ایمیل قبلاً ثبت شده است.", exception.Message);
    }

    [Fact]
    public async Task Last_administrator_cannot_be_removed_or_locked()
    {
        await using AccountFixture fixture = await AccountFixture.CreateAsync();
        AccountUser admin = await fixture.Registry.RegisterAsync(
            "admin@example.com",
            "Test123!",
            [AccountRoles.Administrator],
            CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Registry.UpdateAsync(
            admin.Id,
            [],
            false,
            null,
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Registry.UpdateAsync(
            admin.Id,
            [AccountRoles.Administrator],
            true,
            null,
            CancellationToken.None));

        await fixture.Registry.RegisterAsync(
            "second@example.com",
            "Test123!",
            [AccountRoles.Administrator],
            CancellationToken.None);
        AccountUser updated = await fixture.Registry.UpdateAsync(
            admin.Id,
            [KnowledgeRoles.Reader],
            false,
            null,
            CancellationToken.None) ?? throw new InvalidOperationException("User was not stored.");

        Assert.Equal(["KnowledgeReader"], updated.Roles);
    }

    [Fact]
    public async Task Lock_and_password_change_apply_to_an_existing_user()
    {
        await using AccountFixture fixture = await AccountFixture.CreateAsync();
        AccountUser reader = await fixture.Registry.RegisterAsync(
            "reader@example.com",
            "Test123!",
            [KnowledgeRoles.Reader],
            CancellationToken.None);

        AccountUser locked = await fixture.Registry.UpdateAsync(
            reader.Id,
            [KnowledgeRoles.Manager],
            true,
            "Changed123!",
            CancellationToken.None) ?? throw new InvalidOperationException("User was not stored.");

        Assert.True(locked.IsLocked);
        Assert.Equal(["KnowledgeManager"], locked.Roles);
        ApplicationUser stored = await fixture.Users.FindByIdAsync(reader.Id)
            ?? throw new InvalidOperationException("User was not stored.");
        Assert.True(await fixture.Users.CheckPasswordAsync(stored, "Changed123!"));
    }
}

internal sealed class AccountFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _services;
    private readonly AsyncServiceScope _scope;

    private AccountFixture(SqliteConnection connection, ServiceProvider services, AsyncServiceScope scope)
    {
        _connection = connection;
        _services = services;
        _scope = scope;
    }

    public AccountRegistry Registry => _scope.ServiceProvider.GetRequiredService<AccountRegistry>();

    public UserManager<ApplicationUser> Users => _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    public static async Task<AccountFixture> CreateAsync()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddDbContext<AccountIdentityContext>(options => options.UseSqlite(connection));
        services
            .AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = true)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AccountIdentityContext>();
        services.AddScoped<AccountRegistry>();
        ServiceProvider provider = services.BuildServiceProvider();
        AsyncServiceScope scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AccountIdentityContext>().Database.EnsureCreatedAsync();
        return new AccountFixture(connection, provider, scope);
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

internal sealed class AccountIdentityContext(DbContextOptions<AccountIdentityContext> options)
    : IdentityDbContext<ApplicationUser>(options);
