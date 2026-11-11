using Maf.Client;
using Maf.Client.Chat;
using Maf.Client.Chat.Tools;
using Maf.Client.Authentication;
using Maf.Client.Identity;
using Maf.Client.Knowledge;
using Maf.Client.Mcp;
using Maf.Client.Models;
using AGUI.Client;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.AI;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

string backendUrl = builder.Configuration["BackendUrl"]
    ?? throw new InvalidOperationException("BackendUrl is not configured.");

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<TokenStorage>();
builder.Services.AddScoped<BearerTokenHandler>();
builder.Services.AddScoped<TokenAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    services => services.GetRequiredService<TokenAuthenticationStateProvider>());
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<KnowledgeApiClient>();
builder.Services.AddScoped<McpApiClient>();
builder.Services.AddScoped<ModelProviderApiClient>();
builder.Services.AddScoped<AccountApiClient>();
builder.Services.AddScoped<ChatTraceApiClient>();
builder.Services.AddScoped<PageCaptureJsInterop>();
builder.Services.AddSingleton<ChatSelection>();
builder.Services.AddTransient<ChatModelHandler>();

builder.Services
    .AddHttpClient(
        "Backend",
        client =>
        {
            client.BaseAddress = new Uri(backendUrl);
            client.Timeout = TimeSpan.FromMinutes(6);
        })
        .AddHttpMessageHandler<BearerTokenHandler>()
        .AddHttpMessageHandler<ChatModelHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Backend"));
builder.Services.AddScoped<IChatClient>(sp =>
{
    AGUIChatClient client = new(new AGUIChatClientOptions(
        sp.GetRequiredService<HttpClient>(),
        "ag-ui/general"));
    FunctionInvokingChatClient invocation = client.GetService<FunctionInvokingChatClient>()
        ?? throw new InvalidOperationException("حلقهٔ ابزار صفحه پیدا نشد.");
    invocation.MaximumIterationsPerRequest = 100;
    return client;
});

await builder.Build().RunAsync();
