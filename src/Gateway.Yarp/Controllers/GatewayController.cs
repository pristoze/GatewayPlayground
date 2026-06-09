using BuildingBlocks.Constants;
using BuildingBlocks.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Gateway.Yarp.Controllers;

[ApiController]
[Route("api/gateway")]
public sealed class GatewayController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public GatewayController(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    [HttpGet("info")]
    public ActionResult<ApiResponse<ServiceInfoResponse>> GetInfo()
    {
        return Ok(ApiResponse<ServiceInfoResponse>.Ok(CreateServiceInfo(), GetCorrelationId()));
    }

    [HttpGet]
    public ActionResult<ApiResponse<GatewayStatusResponse>> GetStatus()
    {
        var response = new GatewayStatusResponse(
            "Gateway.Yarp",
            "Microsoft YARP Reverse Proxy",
            "Monolith -> Gateway.Yarp -> Services",
            GetRoutes(),
            GetClusters());

        return Ok(ApiResponse<GatewayStatusResponse>.Ok(response, GetCorrelationId()));
    }

    private ServiceInfoResponse CreateServiceInfo()
    {
        return new ServiceInfoResponse(
            _configuration[ApplicationConstants.ServiceNameConfigurationKey] ?? _environment.ApplicationName,
            _configuration[ApplicationConstants.ServiceDescriptionConfigurationKey] ?? "YARP API Gateway.",
            typeof(GatewayController).Assembly.GetName().Version?.ToString() ?? "1.0.0",
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

    private GatewayRouteResponse[] GetRoutes()
    {
        return _configuration.GetSection("ReverseProxy:Routes")
            .GetChildren()
            .Select(route => new GatewayRouteResponse(
                route.Key,
                route["ClusterId"] ?? string.Empty,
                route.GetSection("Match")["Path"] ?? string.Empty))
            .ToArray();
    }

    private GatewayClusterResponse[] GetClusters()
    {
        return _configuration.GetSection("ReverseProxy:Clusters")
            .GetChildren()
            .Select(cluster => new GatewayClusterResponse(
                cluster.Key,
                cluster.GetSection("Destinations")
                    .GetChildren()
                    .Select(destination => new GatewayDestinationResponse(
                        destination.Key,
                        destination["Address"] ?? string.Empty))
                    .ToArray()))
            .ToArray();
    }

    private string? GetCorrelationId()
    {
        return HttpContext.Items[ApplicationConstants.CorrelationIdItemKey] as string;
    }
}

public sealed record GatewayStatusResponse(
    string Name,
    string Implementation,
    string Architecture,
    IReadOnlyCollection<GatewayRouteResponse> Routes,
    IReadOnlyCollection<GatewayClusterResponse> Clusters);

public sealed record GatewayRouteResponse(
    string RouteId,
    string ClusterId,
    string Path);

public sealed record GatewayClusterResponse(
    string ClusterId,
    IReadOnlyCollection<GatewayDestinationResponse> Destinations);

public sealed record GatewayDestinationResponse(
    string DestinationId,
    string Address);
