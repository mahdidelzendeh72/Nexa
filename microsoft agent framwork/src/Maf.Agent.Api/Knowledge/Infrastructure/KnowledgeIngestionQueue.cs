using System.Threading.Channels;
using Maf.Agent.Api.Knowledge.Application;

namespace Maf.Agent.Api.Knowledge.Infrastructure;

public sealed class KnowledgeIngestionQueue : IKnowledgeIngestionQueue
{
    private readonly Channel<Guid> _channel =
        Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });

    public ValueTask EnqueueAsync(
        Guid documentId,
        CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(documentId, cancellationToken);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
