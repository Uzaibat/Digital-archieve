using System.Security.Claims;
using IDADRS.Core.Entities;
using IDADRS.Core.Interfaces;

namespace IDADRS.API.Middleware;

/// <summary>§5 — Logs every authenticated document request to the access_logs table.</summary>
public sealed class AccessLogMiddleware
{
    private readonly RequestDelegate _next;
    public AccessLogMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, IUnitOfWork uow)
    {
        await _next(ctx);

        if (!ctx.User.Identity?.IsAuthenticated ?? true) return;
        if (!ctx.Request.Path.StartsWithSegments("/api/documents")) return;

        if (!int.TryParse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return;

        var segments   = ctx.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (!int.TryParse(segments?.ElementAtOrDefault(2), out var documentId) || documentId == 0) return;

        var actionType = ctx.Request.Method.ToUpper() switch
        {
            "GET"    => ctx.Request.Query.ContainsKey("download") ? "Download" : "View",
            "POST"   => "Upload",
            "PUT"    => "Update",
            "DELETE" => "Delete",
            _        => "Other",
        };

        try
        {
            await uow.AccessLogs.AddAsync(new AccessLog
            {
                UserId = userId, DocumentId = documentId,
                AccessDate = DateTime.UtcNow, ActionType = actionType,
            });
            await uow.SaveAsync();
        }
        catch { /* never break the response */ }
    }
}
