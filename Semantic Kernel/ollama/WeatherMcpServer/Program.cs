var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<WeatherMcpServer.Services.OpenMeteoClient>();
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithToolsFromAssembly();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    name = "Weather MCP Server",
    endpoint = "/mcp",
    tools = new[] { "GetCityWeatherForecast" }
}));

app.MapMcp("/mcp");

app.Run();
