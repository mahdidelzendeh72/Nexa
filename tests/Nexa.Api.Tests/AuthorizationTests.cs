using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Nexa.Api.Tests;

public sealed class NexaWebFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _keepAlive;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        const string connectionString = "DataSource=nexa-tests;Mode=Memory;Cache=Shared";
        _keepAlive = new SqliteConnection(connectionString);
        _keepAlive.Open();

        builder.UseEnvironment("Development");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Nexa", connectionString);
    }

    protected override void Dispose(bool disposing)
    {
        _keepAlive?.Dispose();
        base.Dispose(disposing);
    }
}

public sealed class AuthorizationTests : IClassFixture<NexaWebFactory>
{
    private readonly NexaWebFactory _factory;

    public AuthorizationTests(NexaWebFactory factory) => _factory = factory;

    [Fact]
    public async Task Agents_api_requires_authentication()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/agents");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_is_anonymous()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
