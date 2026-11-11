namespace Maf.Agent.Api.Chat.Application;

public readonly record struct ChatToolKey(Guid ServerId, string Name);

public static class ChatToolSelection
{
    public const string Header = "X-Chat-Tools";

    public const int MaximumCount = 32;

    public static IReadOnlyList<ChatToolKey> Parse(IEnumerable<string?> values)
    {
        List<ChatToolKey> keys = [];
        HashSet<(Guid ServerId, string Name)> seen = [];
        foreach (string? header in values)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            foreach (string raw in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int split = raw.IndexOf('|');
                if (split <= 0 ||
                    split == raw.Length - 1 ||
                    !Guid.TryParse(raw[..split], out Guid serverId))
                {
                    continue;
                }

                string name;
                try
                {
                    name = Uri.UnescapeDataString(raw[(split + 1)..]).Trim();
                }
                catch (UriFormatException)
                {
                    continue;
                }

                if (name.Length is < 1 or > 256 || !seen.Add((serverId, name)))
                {
                    continue;
                }

                keys.Add(new ChatToolKey(serverId, name));
                if (keys.Count == MaximumCount)
                {
                    return keys;
                }
            }
        }

        return keys;
    }
}
