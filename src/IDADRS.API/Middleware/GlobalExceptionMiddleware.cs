using System.Net;
using System.Text.Json;
using IDADRS.Application.DTOs;

namespace IDADRS.API.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    { _next = next; _logger = logger; }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await _next(ctx); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
            ctx.Response.StatusCode  = (int)HttpStatusCode.InternalServerError;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(
                JsonSerializer.Serialize(ApiResponse<object?>.Fail("An unexpected error occurred.")));
        }
    }
}
