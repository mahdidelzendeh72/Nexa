namespace OllamaChat.Api.Contracts;

public sealed record CreateSessionRequest(string? Title, string? ModelId, float? Temperature);

public sealed record ChatRequest(
    string Message,
    string[]? ToolNames,
    string? ModelId,
    float? Temperature,
    string[]? FileIds);

public sealed record ChatResponse(string Message, string UserId, string SessionId, string ModelId, float Temperature);

public sealed record SessionDto(
    string SessionId,
    string UserId,
    string? Title,
    DateTime CreatedAt,
    int MessageCount,
    string[]? SelectedToolNames,
    string? ModelId,
    float? Temperature,
    IReadOnlyList<SessionFileDto> Files);

public sealed record SessionFileDto(
    string FileId,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime UploadedAt);

public sealed record UploadFileResponse(
    string FileId,
    string FileName,
    string ContentType,
    long SizeBytes);

public sealed record ToolDto(string QualifiedName, string PluginName, string FunctionName, string Description);

public sealed record SetSessionToolsRequest(string[]? ToolNames);

public sealed record SetSessionModelRequest(string? ModelId);

public sealed record SetSessionTemperatureRequest(float? Temperature);

public sealed record ModelDto(string Name);
