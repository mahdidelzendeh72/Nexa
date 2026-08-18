namespace Nexa.Application.Common;

public sealed class NexaException : Exception
{
    public NexaException(string code, string message, int statusCode = 400)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public int StatusCode { get; }

    public static NexaException NotFound(string message) =>
        new(ErrorCodes.NotFound, message, 404);

    public static NexaException Forbidden(string message) =>
        new(ErrorCodes.Forbidden, message, 403);

    public static NexaException Conflict(string message) =>
        new(ErrorCodes.Conflict, message, 409);
}
