using BuildingBlocks.Constants;
using BuildingBlocks.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Monolith.Controllers;

[ApiController]
[Route("api/monolith")]
public sealed class MonolithController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IHttpClientFactory _httpClientFactory;

    public MonolithController(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _environment = environment;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("info")]
    public ActionResult<ApiResponse<ServiceInfoResponse>> GetInfo()
    {
        return Ok(ApiResponse<ServiceInfoResponse>.Ok(CreateServiceInfo(), GetCorrelationId()));
    }

    [HttpGet]
    public ActionResult<ApiResponse<MonolithOverviewResponse>> GetOverview()
    {
        var topology = CreateTopology();
        var response = new MonolithOverviewResponse(
            topology.Mode,
            topology.Flow,
            [
                "Search",
                "Mail",
                "Deduplication",
                "Users"
            ],
            topology.Services,
            "Mode is selected by configuration or environment variables; no source code changes are required.");

        return Ok(ApiResponse<MonolithOverviewResponse>.Ok(response, GetCorrelationId()));
    }

    [HttpGet("architecture")]
    public ActionResult<ApiResponse<ArchitectureTopologyResponse>> GetArchitecture()
    {
        return Ok(ApiResponse<ArchitectureTopologyResponse>.Ok(CreateTopology(), GetCorrelationId()));
    }

    [HttpGet("probe")]
    public async Task<ActionResult<ApiResponse<ArchitectureProbeResponse>>> ProbeServices(CancellationToken cancellationToken)
    {
        var topology = CreateTopology();
        var client = _httpClientFactory.CreateClient("downstream-services");
        var probes = await Task.WhenAll(
            topology.Services.Select(service => ProbeServiceAsync(service, client, cancellationToken)));
        var response = new ArchitectureProbeResponse(topology.Mode, topology.Flow, probes);

        return Ok(ApiResponse<ArchitectureProbeResponse>.Ok(response, GetCorrelationId()));
    }

    private ServiceInfoResponse CreateServiceInfo()
    {
        return new ServiceInfoResponse(
            _configuration[ApplicationConstants.ServiceNameConfigurationKey] ?? _environment.ApplicationName,
            _configuration[ApplicationConstants.ServiceDescriptionConfigurationKey] ?? "Monolith baseline API.",
            typeof(MonolithController).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            _environment.EnvironmentName,
            DateTimeOffset.UtcNow,
            GetCapabilities());
    }

    private string[] GetCapabilities()
    {
        return _configuration.GetSection("Service:Capabilities")
            .GetChildren()
            .Select(value => value.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }

    private ArchitectureTopologyResponse CreateTopology()
    {
        var mode = _configuration["Architecture:Mode"] ?? "ModeA";
        var flow = _configuration["Architecture:Flow"] ?? ResolveFlow(mode);

        return new ArchitectureTopologyResponse(mode, flow, GetDownstreamServices());
    }

    private DownstreamServiceEndpointResponse[] GetDownstreamServices()
    {
        return _configuration.GetSection("DownstreamServices")
            .GetChildren()
            .Select(section => new DownstreamServiceEndpointResponse(
                section.Key,
                section["BaseAddress"] ?? string.Empty,
                section["PathPrefix"] ?? string.Empty,
                section["InfoPath"] ?? "info"))
            .ToArray();
    }

    private async Task<DownstreamServiceProbeResponse> ProbeServiceAsync(
        DownstreamServiceEndpointResponse service,
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var requestUri = service.CreateInfoUri();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            var correlationId = GetCorrelationId();

            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                request.Headers.TryAddWithoutValidation(ApplicationConstants.CorrelationIdHeader, correlationId);
            }

            using var response = await client.SendAsync(request, cancellationToken);

            return new DownstreamServiceProbeResponse(
                service.Name,
                requestUri.ToString(),
                response.IsSuccessStatusCode,
                (int)response.StatusCode,
                response.ReasonPhrase);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new DownstreamServiceProbeResponse(
                service.Name,
                requestUri.ToString(),
                false,
                null,
                exception.Message);
        }
    }

    private static string ResolveFlow(string mode)
    {
        return mode.ToUpperInvariant() switch
        {
            "MODEA" or "A" => "Monolith -> Services",
            "MODEB" or "B" => "Monolith -> Gateway.Yarp -> Services",
            "MODEC" or "C" => "Monolith -> Gateway.Ocelot -> Services",
            _ => "Custom"
        };
    }

    private string? GetCorrelationId()
    {
        return HttpContext.Items[ApplicationConstants.CorrelationIdItemKey] as string;
    }
}

public sealed record MonolithOverviewResponse(
    string ActiveMode,
    string Architecture,
    IReadOnlyCollection<string> OwnedCapabilities,
    IReadOnlyCollection<DownstreamServiceEndpointResponse> DownstreamServices,
    string Notes);

public sealed record ArchitectureTopologyResponse(
    string Mode,
    string Flow,
    IReadOnlyCollection<DownstreamServiceEndpointResponse> Services);

public sealed record DownstreamServiceEndpointResponse(
    string Name,
    string BaseAddress,
    string PathPrefix,
    string InfoPath)
{
    public Uri CreateInfoUri()
    {
        var baseUri = new Uri(EnsureTrailingSlash(BaseAddress), UriKind.Absolute);
        var relativePath = string.Join(
            "/",
            new[] { PathPrefix, InfoPath }
                .Select(part => part.Trim('/'))
                .Where(part => !string.IsNullOrWhiteSpace(part)));

        return new Uri(baseUri, relativePath);
    }

    private static string EnsureTrailingSlash(string value)
    {
        return value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";
    }
}

public sealed record ArchitectureProbeResponse(
    string Mode,
    string Flow,
    IReadOnlyCollection<DownstreamServiceProbeResponse> Services);

public sealed record DownstreamServiceProbeResponse(
    string Name,
    string Url,
    bool IsReachable,
    int? StatusCode,
    string? Message);
