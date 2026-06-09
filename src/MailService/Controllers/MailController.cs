using BuildingBlocks.Constants;
using BuildingBlocks.Responses;
using BuildingBlocks.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MailService.Controllers;

[ApiController]
[Route("api/mail")]
[Authorize(Policy = AuthorizationPolicies.UserOrAdmin)]
public sealed class MailController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public MailController(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    [HttpGet("info")]
    public ActionResult<ApiResponse<ServiceInfoResponse>> GetInfo()
    {
        return Ok(ApiResponse<ServiceInfoResponse>.Ok(CreateServiceInfo(), GetCorrelationId()));
    }

    [HttpGet("admin")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public ActionResult<ApiResponse<RoleProtectedResponse>> GetAdminStatus()
    {
        var response = new RoleProtectedResponse(
            AuthorizationPolicies.AdminRole,
            "MailService admin endpoint.");

        return Ok(ApiResponse<RoleProtectedResponse>.Ok(response, GetCorrelationId()));
    }

    [HttpGet("templates")]
    public ActionResult<ApiResponse<IReadOnlyCollection<MailTemplateResponse>>> GetTemplates()
    {
        MailTemplateResponse[] templates =
        [
            new("welcome", "Welcome message", "Sent when a user is created."),
            new("deduplication-alert", "Deduplication alert", "Sent when duplicate content is detected.")
        ];

        return Ok(ApiResponse<IReadOnlyCollection<MailTemplateResponse>>.Ok(templates, GetCorrelationId()));
    }

    [HttpPost("preview")]
    public ActionResult<ApiResponse<MailPreviewResponse>> Preview([FromBody] MailPreviewRequest request)
    {
        var response = new MailPreviewResponse(
            request.To,
            request.Subject,
            request.Body,
            "Preview only. Mail delivery is not implemented in this playground.");

        return Ok(ApiResponse<MailPreviewResponse>.Ok(response, GetCorrelationId()));
    }

    private ServiceInfoResponse CreateServiceInfo()
    {
        return new ServiceInfoResponse(
            _configuration[ApplicationConstants.ServiceNameConfigurationKey] ?? _environment.ApplicationName,
            _configuration[ApplicationConstants.ServiceDescriptionConfigurationKey] ?? "Mail API.",
            typeof(MailController).Assembly.GetName().Version?.ToString() ?? "1.0.0",
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

public sealed record MailTemplateResponse(
    string Key,
    string Name,
    string Description);

public sealed record MailPreviewRequest(
    string To,
    string Subject,
    string Body);

public sealed record MailPreviewResponse(
    string To,
    string Subject,
    string Body,
    string Notes);

public sealed record RoleProtectedResponse(
    string RequiredRole,
    string Message);
