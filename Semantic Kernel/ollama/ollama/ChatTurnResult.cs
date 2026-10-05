namespace ollama;

public sealed record ChatTurnResult(string Message, string ModelId, float Temperature);
