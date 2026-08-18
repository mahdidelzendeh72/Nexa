namespace Nexa.Domain.Agents;

public sealed record ChatLimits(int? MaxDurationSeconds, int? MaxToolCalls, int? TokenBudget)
{
    public static ChatLimits None { get; } = new(null, null, null);

    public static ChatLimits Create(int? maxDurationSeconds, int? maxToolCalls, int? tokenBudget)
    {
        if (maxDurationSeconds is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDurationSeconds));
        }

        if (maxToolCalls is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxToolCalls));
        }

        if (tokenBudget is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokenBudget));
        }

        return new ChatLimits(maxDurationSeconds, maxToolCalls, tokenBudget);
    }
}
