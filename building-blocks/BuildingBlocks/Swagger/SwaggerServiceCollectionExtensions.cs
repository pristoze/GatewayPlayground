using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace BuildingBlocks.Swagger;

public static class SwaggerServiceCollectionExtensions
{
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services, string serviceName)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = serviceName,
                Version = "v1"
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Paste a Keycloak access token. The header value is sent as: Bearer {token}"
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference("Bearer", document, null),
                    []
                }
            });
        });

        return services;
    }
}
