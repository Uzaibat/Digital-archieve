using FluentValidation;
using IDADRS.Application.DTOs;
namespace IDADRS.Application.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequestDto>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MinimumLength(3).MaximumLength(60)
            .Matches(@"^[a-zA-Z0-9_\-]+$").WithMessage("Username may only contain letters, digits, _ and -.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128)
            .Matches(@"[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain a digit.");
    }
}
