using FluentValidation;
using IDADRS.Application.DTOs;
namespace IDADRS.Application.Validators;

public sealed class DocumentCreateValidator : AbstractValidator<DocumentCreateDto>
{
    private static readonly string[] AllowedTypes =
    [
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "image/png", "image/jpeg",
    ];
    private const long MaxBytes = 50L * 1024 * 1024;

    public DocumentCreateValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.File).NotNull().WithMessage("A file is required.");
        RuleFor(x => x.File.Length).LessThanOrEqualTo(MaxBytes)
            .WithMessage("File must not exceed 50 MB.").When(x => x.File is not null);
        RuleFor(x => x.File.ContentType)
            .Must(t => AllowedTypes.Contains(t))
            .WithMessage("Allowed types: PDF, DOCX, XLSX, PNG, JPG.")
            .When(x => x.File is not null);
    }
}
