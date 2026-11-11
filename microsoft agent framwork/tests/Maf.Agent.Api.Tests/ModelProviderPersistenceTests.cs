using Maf.Agent.Api.Models.Domain;
using Maf.Agent.Api.Models.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Maf.Agent.Api.Tests;

public sealed class ModelProviderPersistenceTests
{
    [Fact]
    public async Task Discovered_models_are_inserted_with_the_provider()
    {
        await using ModelPersistenceContext context = await CreateContextAsync();
        ModelProvider provider = CreateProvider();
        context.Providers.Add(provider);
        provider.RecordDiscovery(
            [
                new DiscoveredModel("gpt-4o", null, null, ModelCapability.LargeLanguage),
                new DiscoveredModel(
                    "llama3.2",
                    DateTimeOffset.Parse("2026-03-01T00:00:00Z"),
                    8192,
                    ModelCapability.Vision | ModelCapability.ToolCalling),
            ],
            DateTimeOffset.UtcNow);

        await context.SaveChangesAsync();

        ModelProvider stored = await context.Providers.Include(item => item.Models).SingleAsync();
        Assert.Equal(2, stored.Models.Count);
        Assert.All(stored.Models, model => Assert.NotEqual(0, model.Id));
        Assert.Equal("secret", stored.ApiKey);
    }

    private static async Task<ModelPersistenceContext> CreateContextAsync()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<ModelPersistenceContext> options =
            new DbContextOptionsBuilder<ModelPersistenceContext>()
                .UseSqlite(connection)
                .Options;
        ModelPersistenceContext context = new(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static ModelProvider CreateProvider() =>
        ModelProvider.Register(
            Guid.NewGuid(),
            "OpenAI",
            ModelProviderKind.OpenAI,
            new Uri("https://api.openai.com"),
            "secret",
            ModelProtocol.ChatCompletions,
            DateTimeOffset.UtcNow);

    private sealed class ModelPersistenceContext(DbContextOptions<ModelPersistenceContext> options)
        : DbContext(options)
    {
        public DbSet<ModelProvider> Providers => Set<ModelProvider>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ConfigureModelProviders();
        }
    }
}
