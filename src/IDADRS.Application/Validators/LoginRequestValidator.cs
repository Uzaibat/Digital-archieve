using FluentValidation;
using IDADRS.Application.DTOs;
namespace IDADRS.Application.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}
