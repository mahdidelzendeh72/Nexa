using Microsoft.EntityFrameworkCore;
using Nexa.Api;
using Nexa.Application;
using Nexa.Infrastructure;
using Nexa.Infrastructure.Persistence;
using Nexa.Web.Components;
using Nexa.Web.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddNexaApplication();
builder.Services.AddNexaInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();

var databaseProvider = builder.Configuration["Database:Provider"] ?? "PostgreSQL";
var healthChecks = builder.Services.AddHealthChecks();
if (!databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
{
    var connectionString = builder.Configuration.GetConnectionString("Nexa");
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        healthChecks.AddNpgSql(connectionString, name: "postgres");
    }
}

var app = builder.Build();

app.UseNexaCorrelationId();
app.UseNexaExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorPages();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapNexaApi();
app.MapOpenApi();
app.MapHealthChecks("/health");

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NexaDbContext>();
    if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        await db.Database.MigrateAsync();
    }

    var seeder = scope.ServiceProvider.GetRequiredService<NexaDataSeeder>();
    await seeder.SeedAsync(CancellationToken.None);
}

app.Run();

public partial class Program;
