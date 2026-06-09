using BuildingBlocks.Constants;
using BuildingBlocks.Logging;
using BuildingBlocks.Middleware;
using Gateway.Ocelot.HealthChecks;
using Gateway.Ocelot.Infrastructure;
using MMLib.SwaggerForOcelot.DependencyInjection;
using MMLib.SwaggerForOcelot.Middleware;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

var serviceName = builder.Configuration[ApplicationConstants.ServiceNameConfigurationKey]
    ?? builder.Environment.ApplicationName;

builder.AddCommonLogging(serviceName);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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

app.MapHealthChecks(ApiRoutes.Health, HealthCheckEndpointOptions.Create());
app.MapControllers();

app.MapWhen(IsOcelotRoute, branch =>
{
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
