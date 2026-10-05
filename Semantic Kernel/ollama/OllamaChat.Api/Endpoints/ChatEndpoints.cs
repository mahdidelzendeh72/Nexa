using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ollama;
using ollama.Configuration;
using ollama.Sessions;
using OllamaChat.Api.Contracts;

namespace OllamaChat.Api.Endpoints;

public static class ChatEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        api.MapGet("/models", async (IOllamaModelCatalog modelCatalog, OllamaSettings settings, CancellationToken cancellationToken) =>
        {
            var models = await modelCatalog.ListModelsAsync(cancellationToken);
            return Results.Ok(new
            {
                defaultModelId = settings.ModelId,
                defaultTemperature = settings.Temperature,
                models = models.Select(name => new ModelDto(name))
            });
        });

        api.MapGet("/tools", (Kernel kernel) =>
        {
            var tools = kernel.Plugins
                .SelectMany(plugin => plugin.Select(function => new ToolDto(
                    ToolNameResolver.ToQualifiedName(function),
                    function.PluginName ?? plugin.Name,
                    function.Name,
                    string.IsNullOrWhiteSpace(function.Description)
                        ? "(no description)"
                        : function.Description)))
                .OrderBy(tool => tool.QualifiedName)
                .ToList();

            return Results.Ok(tools);
        });

        var users = api.MapGroup("/users/{userId}");

        users.MapGet("/sessions", ListSessions);
        users.MapPost("/sessions", CreateSession);
        users.MapGet("/sessions/{sessionId}", GetSession);
        users.MapPut("/sessions/{sessionId}/tools", SetSessionTools);
        users.MapPut("/sessions/{sessionId}/model", SetSessionModel);
        users.MapPut("/sessions/{sessionId}/temperature", SetSessionTemperature);
        users.MapGet("/sessions/{sessionId}/files", ListFiles);
        users.MapPost("/sessions/{sessionId}/files", UploadFile).DisableAntiforgery();
        users.MapPost("/sessions/{sessionId}/chat", Chat);
        users.MapPost("/sessions/{sessionId}/chat/stream", ChatStream);

        return app;
    }

    private static IResult ListSessions(
        string userId,
        IUserChatContext userContext,
        IChatSessionManager sessionManager,
        IChatSessionStore sessionStore)
    {
        ConfigureUser(userContext, sessionManager, userId);
        sessionManager.EnsureActiveSession();

        var sessions = sessionStore.List(userId)
            .Select(ToSessionDto)
            .ToList();

        return Results.Ok(sessions);
    }

    private static IResult CreateSession(
        string userId,
        CreateSessionRequest request,
        IUserChatContext userContext,
        IChatSessionManager sessionManager)
    {
        ConfigureUser(userContext, sessionManager, userId);

        var session = sessionManager.CreateSession(request.Title);
        session.ModelId = NormalizeModelId(request.ModelId);
        session.Temperature = NormalizeTemperature(request.Temperature);
        return Results.Created($"/api/users/{userId}/sessions/{session.SessionId}", ToSessionDto(session));
    }

    private static IResult GetSession(
        string userId,
        string sessionId,
        IUserChatContext userContext,
        IChatSessionManager sessionManager,
        IChatSessionStore sessionStore)
    {
        ConfigureUser(userContext, sessionManager, userId);

        if (!sessionStore.TryGet(userId, sessionId, out var session) || session is null)
        {
            return Results.NotFound(new { message = $"Session '{sessionId}' was not found for user '{userId}'." });
        }

        return Results.Ok(ToSessionDto(session));
    }

    private static IResult SetSessionTools(
        string userId,
        string sessionId,
        SetSessionToolsRequest request,
        IUserChatContext userContext,
        IChatSessionManager sessionManager,
        IChatSessionStore sessionStore)
    {
        ConfigureUser(userContext, sessionManager, userId);

        if (!sessionStore.TryGet(userId, sessionId, out var session) || session is null)
        {
            return Results.NotFound(new { message = $"Session '{sessionId}' was not found for user '{userId}'." });
        }

        session.SelectedToolNames = request.ToolNames;
        return Results.Ok(ToSessionDto(session));
    }

    private static IResult SetSessionModel(
        string userId,
        string sessionId,
        SetSessionModelRequest request,
        IUserChatContext userContext,
        IChatSessionManager sessionManager,
        IChatSessionStore sessionStore)
    {
        ConfigureUser(userContext, sessionManager, userId);

        if (!sessionStore.TryGet(userId, sessionId, out var session) || session is null)
        {
            return Results.NotFound(new { message = $"Session '{sessionId}' was not found for user '{userId}'." });
        }

        session.ModelId = NormalizeModelId(request.ModelId);
        return Results.Ok(ToSessionDto(session));
    }

    private static IResult SetSessionTemperature(
        string userId,
        string sessionId,
        SetSessionTemperatureRequest request,
        IUserChatContext userContext,
        IChatSessionManager sessionManager,
        IChatSessionStore sessionStore)
    {
        ConfigureUser(userContext, sessionManager, userId);

        if (!sessionStore.TryGet(userId, sessionId, out var session) || session is null)
        {
            return Results.NotFound(new { message = $"Session '{sessionId}' was not found for user '{userId}'." });
        }

        session.Temperature = NormalizeTemperature(request.Temperature);
        return Results.Ok(ToSessionDto(session));
    }

    private static IResult ListFiles(
        string userId,
        string sessionId,
        IUserChatContext userContext,
        IChatSessionManager sessionManager,
        IChatSessionStore sessionStore,
        ISessionAttachmentService attachmentService)
    {
        ConfigureUser(userContext, sessionManager, userId);

        if (!sessionStore.TryGet(userId, sessionId, out _))
        {
            return Results.NotFound(new { message = $"Session '{sessionId}' was not found for user '{userId}'." });
        }

        var files = attachmentService.List(userId, sessionId)
            .Select(file => new SessionFileDto(
                file.FileId,
                file.FileName,
                file.ContentType,
                file.SizeBytes,
                file.UploadedAt))
            .ToList();

        return Results.Ok(files);
    }

    private static async Task<IResult> UploadFile(
        string userId,
        string sessionId,
        IFormFile file,
        IUserChatContext userContext,
        IChatSessionManager sessionManager,
        IChatSessionStore sessionStore,
        ISessionAttachmentService attachmentService,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return Results.BadRequest(new { message = "File is required." });
        }

        ConfigureUser(userContext, sessionManager, userId);

        if (!sessionStore.TryGet(userId, sessionId, out _))
        {
            return Results.NotFound(new { message = $"Session '{sessionId}' was not found for user '{userId}'." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await attachmentService.UploadAsync(
                userId,
                sessionId,
                stream,
                file.FileName,
                file.ContentType ?? "application/octet-stream",
                cancellationToken);

            var response = new UploadFileResponse(
                result.FileId,
                result.FileName,
                result.ContentType,
                result.SizeBytes);

            return Results.Created(
                $"/api/users/{userId}/sessions/{sessionId}/files/{result.FileId}",
                response);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> Chat(
        string userId,
        string sessionId,
        ChatRequest request,
        IUserChatContext userContext,
        IChatSessionManager sessionManager,
        IChatSessionStore sessionStore,
        ChatService chatService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message)
            && request.FileIds is not { Length: > 0 })
        {
            return Results.BadRequest(new { message = "Message or fileIds is required." });
        }

        ConfigureUser(userContext, sessionManager, userId);

        if (!sessionStore.TryGet(userId, sessionId, out _))
        {
            return Results.NotFound(new { message = $"Session '{sessionId}' was not found for user '{userId}'." });
        }

        sessionManager.SwitchSession(sessionId);

        try
        {
            var result = await chatService.ChatAsync(
                request.Message,
                request.ToolNames,
                request.ModelId,
                request.Temperature,
                request.FileIds,
                cancellationToken);

            return Results.Ok(new ChatResponse(result.Message, userId, sessionId, result.ModelId, result.Temperature));
        }
        catch (KeyNotFoundException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task ChatStream(
        string userId,
        string sessionId,
        ChatRequest request,
        HttpContext httpContext,
        IUserChatContext userContext,
        IChatSessionManager sessionManager,
        IChatSessionStore sessionStore,
        ChatService chatService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message)
            && request.FileIds is not { Length: > 0 })
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(
                new { message = "Message or fileIds is required." },
                cancellationToken);
            return;
        }

        ConfigureUser(userContext, sessionManager, userId);

        if (!sessionStore.TryGet(userId, sessionId, out _))
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await httpContext.Response.WriteAsJsonAsync(
                new { message = $"Session '{sessionId}' was not found for user '{userId}'." },
                cancellationToken);
            return;
        }

        sessionManager.SwitchSession(sessionId);

        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";
        httpContext.Response.ContentType = "text/event-stream";

        try
        {
            await foreach (var streamEvent in chatService.ChatStreamAsync(
                request.Message,
                request.ToolNames,
                request.ModelId,
                request.Temperature,
                request.FileIds,
                cancellationToken))
            {
                var payload = JsonSerializer.Serialize(streamEvent, JsonOptions);
                await httpContext.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                await httpContext.Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (KeyNotFoundException ex)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsJsonAsync(new { message = ex.Message }, cancellationToken);
        }
    }

    private static void ConfigureUser(
        IUserChatContext userContext,
        IChatSessionManager sessionManager,
        string userId)
    {
        if (!userContext.UserId.Equals(userId, StringComparison.Ordinal))
        {
            sessionManager.SetUser(userId);
        }
    }

    private static string? NormalizeModelId(string? modelId) =>
        string.IsNullOrWhiteSpace(modelId) ? null : modelId.Trim();

    private static float? NormalizeTemperature(float? temperature) =>
        temperature.HasValue ? Math.Clamp(temperature.Value, 0f, 2f) : null;

    private static SessionDto ToSessionDto(ChatSession session) =>
        new(
            session.SessionId,
            session.UserId,
            session.Title,
            session.CreatedAt,
            session.History.Count(message =>
                message.Role == AuthorRole.User
                || message.Role == AuthorRole.Assistant),
            session.SelectedToolNames,
            session.ModelId,
            session.Temperature,
            session.Attachments.Values
                .OrderBy(file => file.UploadedAt)
                .Select(file => new SessionFileDto(
                    file.FileId,
                    file.FileName,
                    file.ContentType,
                    file.Content.Length,
                    file.UploadedAt))
                .ToList());
}
