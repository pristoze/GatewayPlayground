using BuildingBlocks.Constants;
using BuildingBlocks.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Gateway.Ocelot.Controllers;

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
            "Gateway.Ocelot",
            "Ocelot API Gateway",
            "Monolith -> Gateway.Ocelot -> Services",
            GetRoutes(),
            GetSwaggerEndpoints());

        return Ok(ApiResponse<GatewayStatusResponse>.Ok(response, GetCorrelationId()));
    }

    private ServiceInfoResponse CreateServiceInfo()
    {
        return new ServiceInfoResponse(
            _configuration[ApplicationConstants.ServiceNameConfigurationKey] ?? _environment.ApplicationName,
            _configuration[ApplicationConstants.ServiceDescriptionConfigurationKey] ?? "Ocelot API Gateway.",
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
        return _configuration.GetSection("Routes")
            .GetChildren()
            .Select(route =>
            {
                var hostAndPort = route.GetSection("DownstreamHostAndPorts").GetChildren().FirstOrDefault();

                return new GatewayRouteResponse(
                    route["RouteId"] ?? route.Key,
                    route["DownstreamServiceName"] ?? route["SwaggerKey"] ?? string.Empty,
                    route["UpstreamPathTemplate"] ?? string.Empty,
                    route["DownstreamPathTemplate"] ?? string.Empty,
                    route["DownstreamScheme"] ?? "http",
                    hostAndPort?["Host"] ?? string.Empty,
                    hostAndPort?["Port"] ?? string.Empty);
            })
            .ToArray();
    }

    private GatewaySwaggerEndpointResponse[] GetSwaggerEndpoints()
    {
        return _configuration.GetSection("SwaggerEndPoints")
            .GetChildren()
            .Select(endpoint => new GatewaySwaggerEndpointResponse(
                endpoint["Key"] ?? endpoint.Key,
                endpoint.GetSection("Config")
                    .GetChildren()
                    .Select(config => new GatewaySwaggerDocumentResponse(
                        config["Name"] ?? string.Empty,
                        config["Version"] ?? string.Empty,
                        config["Url"] ?? string.Empty))
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
    IReadOnlyCollection<GatewaySwaggerEndpointResponse> SwaggerEndpoints);

public sealed record GatewayRouteResponse(
    string RouteId,
    string ServiceName,
    string UpstreamPathTemplate,
    string DownstreamPathTemplate,
    string DownstreamScheme,
    string DownstreamHost,
    string DownstreamPort);

public sealed record GatewaySwaggerEndpointResponse(
    string Key,
    IReadOnlyCollection<GatewaySwaggerDocumentResponse> Documents);

public sealed record GatewaySwaggerDocumentResponse(
    string Name,
    string Version,
    string Url);
