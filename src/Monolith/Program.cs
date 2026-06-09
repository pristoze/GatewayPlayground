using BuildingBlocks.Constants;
using BuildingBlocks.Logging;
using BuildingBlocks.Middleware;
using BuildingBlocks.Security;
using BuildingBlocks.Swagger;

var builder = WebApplication.CreateBuilder(args);
var serviceName = builder.Configuration[ApplicationConstants.ServiceNameConfigurationKey]
    ?? builder.Environment.ApplicationName;

builder.AddCommonLogging(serviceName);

builder.Services.AddKeycloakAuthentication(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddSwaggerWithJwt(serviceName);
builder.Services.AddHttpClient("downstream-services", client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHealthChecks();

var app = builder.Build();

app.Logger.LogServiceStartup(serviceName, app.Environment);

app.UseCorrelationId();
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks(ApiRoutes.Health);
app.MapControllers();

app.Run();
