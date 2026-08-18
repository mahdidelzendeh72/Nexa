using FluentValidation;
using Nexa.Contracts.Conversations;

namespace Nexa.Application.Conversations;

public sealed class CreateConversationRequestValidator : AbstractValidator<CreateConversationRequest>
{
    public CreateConversationRequestValidator()
    {
        RuleFor(x => x.AgentId).NotEmpty();
        RuleFor(x => x.Title).MaximumLength(200);
    }
}

public sealed class RenameConversationRequestValidator : AbstractValidator<RenameConversationRequest>
{
    public RenameConversationRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    }
}

public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(100_000);
    }
}
