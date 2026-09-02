using Nexa.Application.Common;

namespace Nexa.Application.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    string? Email { get; }
    IReadOnlyCollection<string> Roles { get; }

    Guid RequireUserId()
    {
        if (!IsAuthenticated || UserId == Guid.Empty)
        {
            throw new NexaException(ErrorCodes.Unauthorized, "Authentication is required.", 401);
        }

        return UserId;
    }
}
