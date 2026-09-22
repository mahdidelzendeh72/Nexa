using ollama.Configuration;

namespace ollama.Sessions;

public sealed class SystemPromptBuilder(ChatOptions options) : ISystemPromptBuilder
{
    private const string BaseSystemPromptWithTools = """
        You are a helpful assistant with access to external tools.
        When a user request can be answered using available tools, call the appropriate tool instead of guessing.
        After using tools, summarize the results clearly for the user.
        """;

    private const string BaseSystemPromptPlain = "You are a helpful assistant.";

    public string Build()
    {
        var basePrompt = options.ToolCallingEnabled ? BaseSystemPromptWithTools : BaseSystemPromptPlain;
        return basePrompt + BuildCurrentDateSection();
    }

    private static string BuildCurrentDateSection()
    {
        var utcNow = DateTime.UtcNow;
        var iranNow = ToIranTime(utcNow);

        return $"""


            ## Current date and time
            - UTC: {utcNow:yyyy-MM-dd HH:mm:ss}
            - Iran (UTC+3:30): {iranNow:yyyy-MM-dd HH:mm:ss}

            Always use the dates above as the real current date and time.
            Do not guess today's date from your training data.
            For live UTC time you may call the DateTime-get_utc_now tool.
            """;
    }

    private static DateTime ToIranTime(DateTime utcNow)
    {
        foreach (var zoneId in new[] { "Iran Standard Time", "Asia/Tehran" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out var timeZone))
            {
                return TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
            }
        }

        return utcNow.AddHours(3.5);
    }
}
