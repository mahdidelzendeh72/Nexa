using FluentValidation;
using Nexa.Contracts.Models;

namespace Nexa.Application.Models;

public sealed class CreateModelProfileRequestValidator : AbstractValidator<CreateModelProfileRequest>
{
    public CreateModelProfileRequestValidator()
    {
        RuleFor(x => x.ProviderId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ModelId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Temperature).InclusiveBetween(0, 2).When(x => x.Temperature.HasValue);
        RuleFor(x => x.MaxTokens).GreaterThan(0).When(x => x.MaxTokens.HasValue);
    }
}

public sealed class UpdateModelProfileRequestValidator : AbstractValidator<UpdateModelProfileRequest>
{
    public UpdateModelProfileRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ModelId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Temperature).InclusiveBetween(0, 2).When(x => x.Temperature.HasValue);
        RuleFor(x => x.MaxTokens).GreaterThan(0).When(x => x.MaxTokens.HasValue);
    }
}
