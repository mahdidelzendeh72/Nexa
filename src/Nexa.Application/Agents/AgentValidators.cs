using FluentValidation;
using Nexa.Application.Common;
using Nexa.Contracts.Agents;

namespace Nexa.Application.Agents;

public sealed class CreateAgentRequestValidator : AbstractValidator<CreateAgentRequest>
{
    public CreateAgentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Instructions).NotEmpty().MaximumLength(32_000);
        RuleFor(x => x.ModelProfileId).NotEmpty();
        RuleFor(x => x.MaxDurationSeconds).GreaterThan(0).When(x => x.MaxDurationSeconds.HasValue);
        RuleFor(x => x.MaxToolCalls).GreaterThan(0).When(x => x.MaxToolCalls.HasValue);
        RuleFor(x => x.TokenBudget).GreaterThan(0).When(x => x.TokenBudget.HasValue);
    }
}

public sealed class UpdateAgentRequestValidator : AbstractValidator<UpdateAgentRequest>
{
    public UpdateAgentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class PublishAgentVersionRequestValidator : AbstractValidator<PublishAgentVersionRequest>
{
    public PublishAgentVersionRequestValidator()
    {
        RuleFor(x => x.Instructions).NotEmpty().MaximumLength(32_000);
        RuleFor(x => x.ModelProfileId).NotEmpty();
    }
}

public static class ValidationExtensions
{
    public static async Task EnsureValidAsync<T>(this IValidator<T> validator, T instance, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (result.IsValid)
        {
            return;
        }

        var message = string.Join(" ", result.Errors.Select(e => e.ErrorMessage));
        throw new NexaException(ErrorCodes.ValidationFailed, message, 400);
    }
}
