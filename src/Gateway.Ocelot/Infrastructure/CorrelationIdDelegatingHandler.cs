using BuildingBlocks.Constants;

namespace Gateway.Ocelot.Infrastructure;

public sealed class CorrelationIdDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdDelegatingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            request.Headers.Remove(ApplicationConstants.CorrelationIdHeader);
            request.Headers.TryAddWithoutValidation(ApplicationConstants.CorrelationIdHeader, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }

    private string? GetCorrelationId()
    {
        var context = _httpContextAccessor.HttpContext;

        return context?.Items[ApplicationConstants.CorrelationIdItemKey] as string
            ?? context?.Request.Headers[ApplicationConstants.CorrelationIdHeader].FirstOrDefault();
    }
}
