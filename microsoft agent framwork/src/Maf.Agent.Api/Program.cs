using Maf.Agent.Api.Chat;
using Maf.Agent.Api.Data;
using Maf.Agent.Api.Identity;
using Maf.Agent.Api.Identity.Domain;
using Maf.Agent.Api.Identity.Infrastructure;
using Maf.Agent.Api.Knowledge;
using Maf.Agent.Api.Mcp;
using Maf.Agent.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Claims;

const string ClientCorsPolicy = "Client";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured. Use .NET User Secrets for local development.");

string[] allowedOrigins =
    builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? [];

NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionString);
dataSourceBuilder.UseVector();
NpgsqlDataSource dataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(dataSource);
builder.Services.AddDbContext<ApplicationDbContext>(
    options => options.UseNpgsql(dataSource, npgsql => npgsql.UseVector()));

builder.Services
    .AddAuthentication()
    .AddBearerToken(IdentityConstants.BearerScheme);

builder.Services
    .AddIdentityCore<ApplicationUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddApiEndpoints();

builder.Services.AddAccountFeature();
builder.Services.AddKnowledgeFeature(builder.Configuration);
builder.Services.AddMcpFeature();
builder.Services.AddModelFeature();
builder.Services.AddChatFeature();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        ClientCorsPolicy,
        policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddOpenApi();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
    ApplicationDbContext dbContext =
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
    await IdentitySeedData.InitializeAsync(
        scope.ServiceProvider,
        app.Configuration,
        app.Logger);
}

app.UseHttpsRedirection();
app.UseCors(ClientCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/identity").MapIdentityApi<ApplicationUser>();

app.MapGet(
        "/api/auth/me",
        (ClaimsPrincipal user) => Results.Ok(new
        {
            userId = user.FindFirstValue(ClaimTypes.NameIdentifier),
            email = user.Identity?.Name,
            roles = user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray(),
        }))
    .RequireAuthorization();

app.MapAccountFeature();
app.MapKnowledgeFeature();
app.MapMcpFeature();
app.MapModelFeature();
app.MapChatFeature();

await app.RunAsync();
