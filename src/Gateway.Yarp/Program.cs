using BuildingBlocks.Constants;
using BuildingBlocks.Logging;
using BuildingBlocks.Middleware;
using Gateway.Yarp.HealthChecks;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);
var serviceName = builder.Configuration[ApplicationConstants.ServiceNameConfigurationKey]
    ?? builder.Environment.ApplicationName;

builder.AddCommonLogging(serviceName);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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

app.MapHealthChecks(ApiRoutes.Health, HealthCheckEndpointOptions.Create());
app.MapControllers();
app.MapReverseProxy();

app.Run();
