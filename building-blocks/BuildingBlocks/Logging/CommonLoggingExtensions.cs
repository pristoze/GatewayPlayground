using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Logging;

public static class CommonLoggingExtensions
{
    public static WebApplicationBuilder AddCommonLogging(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });
        builder.Logging.Configure(options =>
        {
            options.ActivityTrackingOptions =
                ActivityTrackingOptions.TraceId |
                ActivityTrackingOptions.SpanId |
                ActivityTrackingOptions.ParentId;
        });

        builder.Services.AddSingleton(new ServiceMetadata(serviceName));

        return builder;
    }

    public static void LogServiceStartup(
        this ILogger logger,
        string serviceName,
        IWebHostEnvironment environment)
    {
        logger.LogInformation(
            "Starting {ServiceName} in {EnvironmentName}",
            serviceName,
            environment.EnvironmentName);
    }
}

public sealed record ServiceMetadata(string Name);
