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
    public ActionResult<ApiResponse<GatewayPlaceholderResponse>> GetStatus()
    {
        var response = new GatewayPlaceholderResponse(
            "Gateway.Ocelot",
            "Not implemented",
            "Ocelot package, route configuration, and gateway middleware are intentionally not added yet.",
            [
                "Map service routes",
                "Evaluate aggregation support",
                "Compare gateway policy behavior"
            ]);

        return Ok(ApiResponse<GatewayPlaceholderResponse>.Ok(response, GetCorrelationId()));
    }

    private ServiceInfoResponse CreateServiceInfo()
    {
        return new ServiceInfoResponse(
            _configuration[ApplicationConstants.ServiceNameConfigurationKey] ?? _environment.ApplicationName,
            _configuration[ApplicationConstants.ServiceDescriptionConfigurationKey] ?? "Ocelot gateway placeholder API.",
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

    private string? GetCorrelationId()
    {
        return HttpContext.Items[ApplicationConstants.CorrelationIdItemKey] as string;
    }
}

public sealed record GatewayPlaceholderResponse(
    string Name,
    string ImplementationStatus,
    string Notes,
    IReadOnlyCollection<string> PlannedCapabilities);
