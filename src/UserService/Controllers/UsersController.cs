using BuildingBlocks.Constants;
using BuildingBlocks.Responses;
using BuildingBlocks.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UserService.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = AuthorizationPolicies.UserOrAdmin)]
public sealed class UsersController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public UsersController(IConfiguration configuration, IWebHostEnvironment environment)
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
            "UserService admin endpoint.");

        return Ok(ApiResponse<RoleProtectedResponse>.Ok(response, GetCorrelationId()));
    }

    [HttpGet]
    public ActionResult<ApiResponse<IReadOnlyCollection<UserSummaryResponse>>> GetUsers()
    {
        UserSummaryResponse[] users =
        [
            new(Guid.Parse("4cfa7251-9a7e-4e71-97b1-6f24a602d315"), "Ada Lovelace", "ada@example.test", "Active"),
            new(Guid.Parse("bd9672b4-8916-426b-a39f-a71516b1df76"), "Grace Hopper", "grace@example.test", "Active")
        ];

        return Ok(ApiResponse<IReadOnlyCollection<UserSummaryResponse>>.Ok(users, GetCorrelationId()));
    }

    private ServiceInfoResponse CreateServiceInfo()
    {
        return new ServiceInfoResponse(
            _configuration[ApplicationConstants.ServiceNameConfigurationKey] ?? _environment.ApplicationName,
            _configuration[ApplicationConstants.ServiceDescriptionConfigurationKey] ?? "User API.",
            typeof(UsersController).Assembly.GetName().Version?.ToString() ?? "1.0.0",
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

public sealed record UserSummaryResponse(
    Guid Id,
    string DisplayName,
    string Email,
    string Status);

public sealed record RoleProtectedResponse(
    string RequiredRole,
    string Message);
