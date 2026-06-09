using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Gateway.Ocelot.HealthChecks;

public sealed class OcelotDownstreamHealthCheck : IHealthCheck
{
    public const string HttpClientName = "ocelot-downstream-health";

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OcelotDownstreamHealthCheck> _logger;

    public OcelotDownstreamHealthCheck(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<OcelotDownstreamHealthCheck> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var routes = GetDownstreamRoutes();
        if (routes.Length == 0)
        {
            return HealthCheckResult.Unhealthy("No Ocelot downstream routes are configured.");
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var checks = await Task.WhenAll(routes.Select(route => CheckRouteAsync(route, client, cancellationToken)));
        var data = checks.ToDictionary(check => check.ServiceName, check => (object)check);
        var failedChecks = checks.Where(check => !check.IsHealthy).ToArray();

        return failedChecks.Length == 0
            ? HealthCheckResult.Healthy("All Ocelot downstream services are reachable.", data)
            : HealthCheckResult.Unhealthy("One or more Ocelot downstream services are unreachable.", data: data);
    }

    private DownstreamRoute[] GetDownstreamRoutes()
    {
        return _configuration.GetSection("Routes")
            .GetChildren()
            .Select(DownstreamRoute.FromConfiguration)
            .Where(route => route is not null)
            .Select(route => route!)
            .GroupBy(route => route.ServiceName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private async Task<DownstreamServiceHealth> CheckRouteAsync(
        DownstreamRoute route,
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var healthUri = route.CreateHealthUri();

        try
        {
            using var response = await client.GetAsync(healthUri, cancellationToken);

            return response.IsSuccessStatusCode
                ? DownstreamServiceHealth.Healthy(route.ServiceName, healthUri.ToString(), (int)response.StatusCode)
                : DownstreamServiceHealth.Unhealthy(
                    route.ServiceName,
                    healthUri.ToString(),
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(exception, "Ocelot downstream health check failed for {ServiceName}", route.ServiceName);

            return DownstreamServiceHealth.Unhealthy(route.ServiceName, healthUri.ToString(), exception.Message);
        }
    }
}

public sealed record DownstreamRoute(
    string ServiceName,
    string Scheme,
    string Host,
    int Port)
{
    public static DownstreamRoute? FromConfiguration(IConfigurationSection route)
    {
        var hostAndPort = route.GetSection("DownstreamHostAndPorts").GetChildren().FirstOrDefault();
        var host = hostAndPort?["Host"];

        return string.IsNullOrWhiteSpace(host) || !int.TryParse(hostAndPort?["Port"], out var port)
            ? null
            : new DownstreamRoute(
                route["DownstreamServiceName"] ?? route["SwaggerKey"] ?? route["RouteId"] ?? route.Key,
                route["DownstreamScheme"] ?? "http",
                host,
                port);
    }

    public Uri CreateHealthUri()
    {
        var builder = new UriBuilder(Scheme, Host, Port, "health");

        return builder.Uri;
    }
}

public sealed record DownstreamServiceHealth(
    string ServiceName,
    string HealthUrl,
    bool IsHealthy,
    int? StatusCode,
    string? Message)
{
    public static DownstreamServiceHealth Healthy(string serviceName, string healthUrl, int statusCode)
    {
        return new DownstreamServiceHealth(serviceName, healthUrl, true, statusCode, null);
    }

    public static DownstreamServiceHealth Unhealthy(string serviceName, string healthUrl, string message)
    {
        return new DownstreamServiceHealth(serviceName, healthUrl, false, null, message);
    }
}
