namespace ollama;

public sealed record ChatStreamEvent(
    string Type,
    string? Text = null,
    string? ModelId = null,
    float? Temperature = null,
    string? Message = null);
