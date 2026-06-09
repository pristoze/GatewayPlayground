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

    public MonolithController(IConfiguration configuration, IWebHostEnvironment environment)
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
    public ActionResult<ApiResponse<MonolithOverviewResponse>> GetOverview()
    {
        var response = new MonolithOverviewResponse(
            "Monolith baseline",
            [
                "Search",
                "Mail",
                "Deduplication",
                "Users"
            ],
            "Single-process comparison point for gateway and service split evaluation.");

        return Ok(ApiResponse<MonolithOverviewResponse>.Ok(response, GetCorrelationId()));
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

    private string? GetCorrelationId()
    {
        return HttpContext.Items[ApplicationConstants.CorrelationIdItemKey] as string;
    }
}

public sealed record MonolithOverviewResponse(
    string Mode,
    IReadOnlyCollection<string> OwnedCapabilities,
    string Notes);
