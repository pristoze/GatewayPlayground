using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Gateway.Yarp.HealthChecks;

public sealed class ReverseProxyClusterHealthCheck : IHealthCheck
{
    public const string HttpClientName = "downstream-health";

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ReverseProxyClusterHealthCheck> _logger;

    public ReverseProxyClusterHealthCheck(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<ReverseProxyClusterHealthCheck> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var clusters = _configuration.GetSection("ReverseProxy:Clusters").GetChildren().ToArray();
        if (clusters.Length == 0)
        {
            return HealthCheckResult.Unhealthy("No YARP clusters are configured.");
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var checks = await Task.WhenAll(clusters.Select(cluster => CheckClusterAsync(cluster, client, cancellationToken)));
        var data = checks.ToDictionary(check => check.ClusterId, check => (object)check);
        var failedChecks = checks.Where(check => !check.IsHealthy).ToArray();

        return failedChecks.Length == 0
            ? HealthCheckResult.Healthy("All downstream services are reachable.", data)
            : HealthCheckResult.Unhealthy("One or more downstream services are unreachable.", data: data);
    }

    private async Task<DownstreamClusterHealth> CheckClusterAsync(
        IConfigurationSection cluster,
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var clusterId = cluster.Key;
        var destination = cluster.GetSection("Destinations").GetChildren().FirstOrDefault();
        var address = destination?["Address"];

        if (string.IsNullOrWhiteSpace(address))
        {
            return DownstreamClusterHealth.Unhealthy(clusterId, null, "Cluster has no configured destination address.");
        }

        var healthUri = CreateHealthUri(address);

        try
        {
            using var response = await client.GetAsync(healthUri, cancellationToken);

            return response.IsSuccessStatusCode
                ? DownstreamClusterHealth.Healthy(clusterId, healthUri.ToString(), (int)response.StatusCode)
                : DownstreamClusterHealth.Unhealthy(
                    clusterId,
                    healthUri.ToString(),
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(exception, "Downstream health check failed for {ClusterId}", clusterId);

            return DownstreamClusterHealth.Unhealthy(clusterId, healthUri.ToString(), exception.Message);
        }
    }

    private static Uri CreateHealthUri(string destinationAddress)
    {
        return new Uri(new Uri(destinationAddress, UriKind.Absolute), "health");
    }
}

public sealed record DownstreamClusterHealth(
    string ClusterId,
    string? HealthUrl,
    bool IsHealthy,
    int? StatusCode,
    string? Message)
{
    public static DownstreamClusterHealth Healthy(string clusterId, string healthUrl, int statusCode)
    {
        return new DownstreamClusterHealth(clusterId, healthUrl, true, statusCode, null);
    }

    public static DownstreamClusterHealth Unhealthy(string clusterId, string? healthUrl, string message)
    {
        return new DownstreamClusterHealth(clusterId, healthUrl, false, null, message);
    }
}
