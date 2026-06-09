using BuildingBlocks.Constants;
using BuildingBlocks.Logging;
using BuildingBlocks.Middleware;
using BuildingBlocks.Security;
using BuildingBlocks.Swagger;
using Gateway.Ocelot.HealthChecks;
using Gateway.Ocelot.Infrastructure;
using MMLib.SwaggerForOcelot.DependencyInjection;
using MMLib.SwaggerForOcelot.Middleware;
using Microsoft.AspNetCore.Authentication;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

var serviceName = builder.Configuration[ApplicationConstants.ServiceNameConfigurationKey]
    ?? builder.Environment.ApplicationName;

builder.AddCommonLogging(serviceName);

builder.Services.AddKeycloakAuthentication(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddSwaggerWithJwt(serviceName);
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdDelegatingHandler>();
builder.Services.AddHttpClient(OcelotDownstreamHealthCheck.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services
    .AddHealthChecks()
    .AddCheck<OcelotDownstreamHealthCheck>("downstream-services");
builder.Services
    .AddOcelot(builder.Configuration)
    .AddDelegatingHandler<CorrelationIdDelegatingHandler>(true);
builder.Services.AddSwaggerForOcelot(builder.Configuration);

var app = builder.Build();

app.Logger.LogServiceStartup(serviceName, app.Environment);

app.UseCorrelationId();
app.UseSwagger();
app.UseSwaggerForOcelotUI(options =>
{
    options.PathToSwaggerGenerator = "/swagger/docs";
});
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks(ApiRoutes.Health, HealthCheckEndpointOptions.Create());
app.MapControllers();

app.MapWhen(IsOcelotRoute, branch =>
{
    branch.Use(RequireUserOrAdminAsync);
    branch.UseOcelot().GetAwaiter().GetResult();
});

app.Run();

static bool IsOcelotRoute(HttpContext context)
{
    var path = context.Request.Path;

    return path.StartsWithSegments("/api/search")
        || path.StartsWithSegments("/api/mail")
        || path.StartsWithSegments("/api/deduplication")
        || path.StartsWithSegments("/api/users");
}

static async Task RequireUserOrAdminAsync(HttpContext context, RequestDelegate next)
{
    if (context.User.Identity?.IsAuthenticated != true)
    {
        await context.ChallengeAsync();
        return;
    }

    if (!context.User.IsInRole(AuthorizationPolicies.AdminRole) && !context.User.IsInRole(AuthorizationPolicies.UserRole))
    {
        await context.ForbidAsync();
        return;
    }

    await next(context);
}
