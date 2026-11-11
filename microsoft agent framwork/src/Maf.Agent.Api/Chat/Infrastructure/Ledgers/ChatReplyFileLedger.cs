using System.Collections.Concurrent;
using Maf.Agent.Api.Chat.Domain;

namespace Maf.Agent.Api.Chat.Infrastructure.Ledgers;

public sealed class ChatReplyFileLedger
{
    private readonly ConcurrentDictionary<string, List<ChatFile>> _pending = new(StringComparer.Ordinal);

    public void Add(string threadId, IReadOnlyList<ChatFile> files)
    {
        if (string.IsNullOrWhiteSpace(threadId) || files.Count == 0)
        {
            return;
        }

        List<ChatFile> pending = _pending.GetOrAdd(threadId, _ => []);
        lock (pending)
        {
            foreach (ChatFile file in files)
            {
                if (pending.Count >= ChatFile.MaxPerMessage)
                {
                    return;
                }

                pending.Add(file);
            }
        }
    }

    public IReadOnlyList<ChatFile> Take(string threadId, IReadOnlyList<ChatFile> latest)
    {
        _pending.TryRemove(threadId, out List<ChatFile>? pending);
        List<ChatFile> files = [];
        if (pending is not null)
        {
            lock (pending)
            {
                files.AddRange(pending);
            }
        }

        foreach (ChatFile file in latest)
        {
            if (files.Count >= ChatFile.MaxPerMessage)
            {
                break;
            }

            files.Add(file);
        }

        return files;
    }
}
