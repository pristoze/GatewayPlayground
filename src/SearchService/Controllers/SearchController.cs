using BuildingBlocks.Constants;
using BuildingBlocks.Responses;
using Microsoft.AspNetCore.Mvc;

namespace SearchService.Controllers;

[ApiController]
[Route("api/search")]
public sealed class SearchController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public SearchController(IConfiguration configuration, IWebHostEnvironment environment)
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
    public ActionResult<ApiResponse<SearchResponse>> Search([FromQuery] string? q)
    {
        var query = string.IsNullOrWhiteSpace(q) ? "gateway" : q.Trim();
        var response = new SearchResponse(
            query,
            [
                new SearchResult("doc-001", "Gateway evaluation notes", "KnowledgeBase", $"Result for '{query}'."),
                new SearchResult("doc-002", "Service decomposition checklist", "KnowledgeBase", "Baseline data returned by SearchService.")
            ]);

        return Ok(ApiResponse<SearchResponse>.Ok(response, GetCorrelationId()));
    }

    private ServiceInfoResponse CreateServiceInfo()
    {
        return new ServiceInfoResponse(
            _configuration[ApplicationConstants.ServiceNameConfigurationKey] ?? _environment.ApplicationName,
            _configuration[ApplicationConstants.ServiceDescriptionConfigurationKey] ?? "Search API.",
            typeof(SearchController).Assembly.GetName().Version?.ToString() ?? "1.0.0",
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

public sealed record SearchResponse(
    string Query,
    IReadOnlyCollection<SearchResult> Results);

public sealed record SearchResult(
    string Id,
    string Title,
    string Source,
    string Snippet);
