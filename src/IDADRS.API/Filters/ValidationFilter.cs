using IDADRS.Application.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IDADRS.API.Filters;

public sealed class ValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext ctx)
    {
        if (!ctx.ModelState.IsValid)
        {
            var errors = ctx.ModelState
                .Where(k => k.Value?.Errors.Count > 0)
                .SelectMany(k => k.Value!.Errors.Select(e => e.ErrorMessage));
            ctx.Result = new BadRequestObjectResult(
                ApiResponse<object?>.Fail(string.Join(" | ", errors)));
        }
    }
    public void OnActionExecuted(ActionExecutedContext ctx) { }
}
