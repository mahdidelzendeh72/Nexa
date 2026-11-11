using System.Security.Claims;
using Maf.Agent.Api.Chat.Infrastructure.Ledgers;
using Microsoft.AspNetCore.Http.Features;

namespace Maf.Agent.Api.Chat.Infrastructure.Middleware;

public sealed class AgUiEventLogMiddleware(RequestDelegate next, ChatTraceLedger traces)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/ag-ui") ||
            context.Features.Get<IHttpResponseBodyFeature>() is not IHttpResponseBodyFeature body)
        {
            await next(context);
            return;
        }

        var logging = new EventLoggingStream(body.Stream, context, traces);
        context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(logging, body));
        try
        {
            await next(context);
        }
        finally
        {
            logging.FlushPending();
            context.Features.Set(body);
        }
    }

    private sealed class EventLoggingStream(Stream inner, HttpContext context, ChatTraceLedger traces) : Stream
    {
        private readonly MemoryStream _pending = new();
        private string? _threadId;
        private string? _runId;

        public void FlushPending() => Emit(flushTail: true);

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => inner.CanWrite;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            inner.Write(buffer, offset, count);
            _pending.Write(buffer, offset, count);
            Emit(flushTail: false);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await inner.WriteAsync(buffer, cancellationToken);
            _pending.Write(buffer.Span);
            Emit(flushTail: false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                FlushPending();
            }

            base.Dispose(disposing);
        }

        private void Emit(bool flushTail)
        {
            byte[] buffered = _pending.ToArray();
            int consumed = 0;
            while (TryTakeFrame(buffered.AsSpan(consumed), out int length, out int separatorLength))
            {
                Note(buffered.AsSpan(consumed, length));
                consumed += length + separatorLength;
            }

            if (flushTail && consumed < buffered.Length)
            {
                Note(buffered.AsSpan(consumed));
                consumed = buffered.Length;
            }

            _pending.SetLength(0);
            if (consumed < buffered.Length)
            {
                _pending.Write(buffered, consumed, buffered.Length - consumed);
            }
        }

        private void Note(ReadOnlySpan<byte> frame)
        {
            if (!AgUiTraceSignals.TryRead(frame, out AgUiTraceSignal signal))
            {
                return;
            }

            switch (signal.Type)
            {
                case "RUN_STARTED" when signal.ThreadId is not null && signal.RunId is not null:
                    _threadId = signal.ThreadId;
                    _runId = signal.RunId;
                    traces.Begin(_threadId, context.User.FindFirstValue(ClaimTypes.NameIdentifier), _runId);
                    break;
                case "RUN_FINISHED" when _threadId is not null:
                    _runId = signal.RunId ?? _runId;
                    if (_runId is not null)
                    {
                        traces.Finish(
                            _threadId,
                            _runId,
                            signal.Outcome,
                            signal.Model,
                            signal.InputTokens,
                            signal.OutputTokens,
                            signal.ReasoningTokens,
                            signal.TotalTokens);
                    }

                    break;
                case "TOOL_CALL_START" or "TOOL_CALL_ARGS" or "TOOL_CALL_END" or "TOOL_CALL_RESULT"
                    when _threadId is not null && _runId is not null:
                    traces.Step(_threadId, _runId, "tool", signal.RefId, signal.Name, signal.Size);
                    break;
                case "TEXT_MESSAGE_START" or "TEXT_MESSAGE_CONTENT" or "TEXT_MESSAGE_END" or "TEXT_MESSAGE_CHUNK"
                    when _threadId is not null && _runId is not null:
                    traces.Step(_threadId, _runId, "message", signal.RefId, signal.Name, signal.Size);
                    break;
                default:
                    if (_threadId is not null &&
                        _runId is not null &&
                        signal.Type.StartsWith("REASONING_", StringComparison.Ordinal))
                    {
                        traces.Step(_threadId, _runId, "reasoning", signal.RefId, null, signal.Size);
                    }

                    break;
            }
        }

        private static bool TryTakeFrame(ReadOnlySpan<byte> buffer, out int length, out int separatorLength)
        {
            int newline = buffer.IndexOf("\n\n"u8);
            int newlineLength = 2;
            int carriage = buffer.IndexOf("\r\n\r\n"u8);
            if (carriage >= 0 && (newline < 0 || carriage < newline))
            {
                newline = carriage;
                newlineLength = 4;
            }

            if (newline < 0)
            {
                length = 0;
                separatorLength = 0;
                return false;
            }

            length = newline;
            separatorLength = newlineLength;
            return true;
        }
    }
}

public sealed class AgUiEventLogStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.UseMiddleware<AgUiEventLogMiddleware>();
            next(app);
        };
}
