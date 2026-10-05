using Microsoft.Extensions.Hosting;

namespace ollama;

public sealed class OllamaBootstrapHostedService(OllamaBootstrap bootstrap) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        bootstrap.InitializeAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) =>
        bootstrap.DisposeAsync().AsTask();
}
