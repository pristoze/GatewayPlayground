using BuildingBlocks.Constants;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Middleware;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        context.Items[ApplicationConstants.CorrelationIdItemKey] = correlationId;
        context.Request.Headers[ApplicationConstants.CorrelationIdHeader] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[ApplicationConstants.CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            [ApplicationConstants.CorrelationIdItemKey] = correlationId
        }))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var headerValue = context.Request.Headers[ApplicationConstants.CorrelationIdHeader].FirstOrDefault();

        return string.IsNullOrWhiteSpace(headerValue)
            ? Guid.NewGuid().ToString("N")
            : headerValue;
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
