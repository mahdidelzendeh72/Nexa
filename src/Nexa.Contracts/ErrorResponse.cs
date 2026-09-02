namespace Nexa.Contracts;

public sealed record ErrorResponse(string Code, string Message, string CorrelationId);
