using BuildingBlocks.Constants;
using BuildingBlocks.Responses;
using Microsoft.AspNetCore.Mvc;

namespace DeduplicationService.Controllers;

[ApiController]
[Route("api/deduplication")]
public sealed class DeduplicationController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public DeduplicationController(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    [HttpGet("info")]
    public ActionResult<ApiResponse<ServiceInfoResponse>> GetInfo()
    {
        return Ok(ApiResponse<ServiceInfoResponse>.Ok(CreateServiceInfo(), GetCorrelationId()));
    }

    [HttpPost("check")]
    public ActionResult<ApiResponse<DeduplicationResponse>> Check([FromBody] DeduplicationRequest request)
    {
        var values = request.Values ?? [];
        var duplicates = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new DuplicateValue(group.Key, group.Count()))
            .ToArray();

        var response = new DeduplicationResponse(
            values.Count,
            duplicates.Length,
            duplicates);

        return Ok(ApiResponse<DeduplicationResponse>.Ok(response, GetCorrelationId()));
    }

    private ServiceInfoResponse CreateServiceInfo()
    {
        return new ServiceInfoResponse(
            _configuration[ApplicationConstants.ServiceNameConfigurationKey] ?? _environment.ApplicationName,
            _configuration[ApplicationConstants.ServiceDescriptionConfigurationKey] ?? "Deduplication API.",
            typeof(DeduplicationController).Assembly.GetName().Version?.ToString() ?? "1.0.0",
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

public sealed record DeduplicationRequest(IReadOnlyCollection<string>? Values);

public sealed record DeduplicationResponse(
    int InputCount,
    int DuplicateGroupCount,
    IReadOnlyCollection<DuplicateValue> Duplicates);

public sealed record DuplicateValue(
    string Value,
    int Count);
