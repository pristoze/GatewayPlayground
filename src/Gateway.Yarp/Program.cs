using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Constants;
using BuildingBlocks.Logging;
using BuildingBlocks.Middleware;
using BuildingBlocks.Security;
using BuildingBlocks.Swagger;
using Gateway.Yarp.HealthChecks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);
var serviceName = builder.Configuration[ApplicationConstants.ServiceNameConfigurationKey]
    ?? builder.Environment.ApplicationName;

builder.AddCommonLogging(serviceName);

builder.Services.AddKeycloakAuthentication(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddSwaggerWithJwt(serviceName);
builder.Services.AddHttpClient(ReverseProxyClusterHealthCheck.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services
    .AddHealthChecks()
    .AddCheck<ReverseProxyClusterHealthCheck>("downstream-services");
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transformBuilderContext =>
    {
        transformBuilderContext.AddRequestTransform(transformContext =>
        {
            var correlationId = transformContext.HttpContext.Items[ApplicationConstants.CorrelationIdItemKey] as string
                ?? transformContext.HttpContext.Request.Headers[ApplicationConstants.CorrelationIdHeader].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                transformContext.ProxyRequest.Headers.Remove(ApplicationConstants.CorrelationIdHeader);
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation(
                    ApplicationConstants.CorrelationIdHeader,
                    correlationId);
            }

            ApplyTrustedGatewayClaims(transformContext);

            return ValueTask.CompletedTask;
        });
    });

var app = builder.Build();

app.Logger.LogServiceStartup(serviceName, app.Environment);

app.UseCorrelationId();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Gateway.Yarp");

    foreach (var endpoint in app.Configuration.GetSection("Swagger:Endpoints").GetChildren())
    {
        var name = endpoint["Name"];
        var url = endpoint["Url"];

        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(url))
        {
            options.SwaggerEndpoint(url, name);
        }
    }
});
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseWhen(IsProtectedServiceRoute, branch =>
{
    branch.Use(AuthorizeGatewayRouteAsync);
});

app.MapHealthChecks(ApiRoutes.Health, HealthCheckEndpointOptions.Create());
app.MapControllers();
app.MapReverseProxy();

app.Run();

static async Task AuthorizeGatewayRouteAsync(HttpContext context, RequestDelegate next)
{
    var policyName = IsAdminServiceRoute(context.Request.Path)
        ? AuthorizationPolicies.AdminOnly
        : AuthorizationPolicies.UserOrAdmin;
    var authorizationService = context.RequestServices.GetRequiredService<IAuthorizationService>();

    if (context.User.Identity?.IsAuthenticated != true)
    {
        await context.ChallengeAsync();
        return;
    }

    var result = await authorizationService.AuthorizeAsync(context.User, policyName);
    if (!result.Succeeded)
    {
        await context.ForbidAsync();
        return;
    }

    await next(context);
}

static bool IsProtectedServiceRoute(HttpContext context)
{
    var path = context.Request.Path;

    return path.StartsWithSegments("/api/search")
        || path.StartsWithSegments("/api/mail")
        || path.StartsWithSegments("/api/deduplication")
        || path.StartsWithSegments("/api/users");
}

static bool IsAdminServiceRoute(PathString path)
{
    return path.StartsWithSegments("/api/search/admin")
        || path.StartsWithSegments("/api/mail/admin")
        || path.StartsWithSegments("/api/deduplication/admin")
        || path.StartsWithSegments("/api/users/admin");
}

static void ApplyTrustedGatewayClaims(RequestTransformContext transformContext)
{
    transformContext.ProxyRequest.Headers.Remove("Authorization");

    foreach (var header in GatewayClaimHeaders.All)
    {
        transformContext.ProxyRequest.Headers.Remove(header);
    }

    var user = transformContext.HttpContext.User;
    var roles = user.FindAll(ClaimTypes.Role)
        .Select(claim => claim.Value)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    var userId = user.FindFirstValue("sub") ?? string.Empty;
    var userName = user.FindFirstValue("preferred_username")
        ?? user.FindFirstValue(ClaimTypes.Name)
        ?? string.Empty;

    transformContext.ProxyRequest.Headers.TryAddWithoutValidation(GatewayClaimHeaders.Authenticated, "true");
    transformContext.ProxyRequest.Headers.TryAddWithoutValidation(GatewayClaimHeaders.UserId, userId);
    transformContext.ProxyRequest.Headers.TryAddWithoutValidation(GatewayClaimHeaders.UserName, userName);
    transformContext.ProxyRequest.Headers.TryAddWithoutValidation(GatewayClaimHeaders.Roles, string.Join(",", roles));
    transformContext.ProxyRequest.Headers.TryAddWithoutValidation(GatewayClaimHeaders.Claims, EncodeClaims(user));
}

static string EncodeClaims(ClaimsPrincipal user)
{
    var claims = user.Claims
        .Select(claim => new GatewayClaim(claim.Type, claim.Value))
        .ToArray();
    var json = JsonSerializer.Serialize(claims);
    var bytes = Encoding.UTF8.GetBytes(json);

    return Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}

public sealed record GatewayClaim(string Type, string Value);
