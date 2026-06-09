using BuildingBlocks.Constants;
using BuildingBlocks.Logging;
using BuildingBlocks.Middleware;

var builder = WebApplication.CreateBuilder(args);
var serviceName = builder.Configuration[ApplicationConstants.ServiceNameConfigurationKey]
    ?? builder.Environment.ApplicationName;

builder.AddCommonLogging(serviceName);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.Logger.LogServiceStartup(serviceName, app.Environment);

app.UseCorrelationId();
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();

app.MapHealthChecks(ApiRoutes.Health);
app.MapControllers();

app.Run();
