using CardActions.API.DTOs;
using FluentValidation;

namespace CardActions.API.Validators;

public class CardActionsRequestValidator : AbstractValidator<CardActionsRequest>
{
    public CardActionsRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required")
            .MinimumLength(3).WithMessage("UserId must be at least 3 characters long")
            .MaximumLength(50).WithMessage("UserId cannot exceed 50 characters")
            .Matches("^[a-zA-Z0-9_-]+$").WithMessage("UserId can only contain letters, numbers, hyphens, and underscores");

        RuleFor(x => x.CardNumber)
            .NotEmpty().WithMessage("CardNumber is required")
            .MinimumLength(3).WithMessage("CardNumber must be at least 3 characters long")
            .MaximumLength(50).WithMessage("CardNumber cannot exceed 50 characters")
            .Matches("^[a-zA-Z0-9_-]+$").WithMessage("CardNumber can only contain letters, numbers, hyphens, and underscores");
    }
}