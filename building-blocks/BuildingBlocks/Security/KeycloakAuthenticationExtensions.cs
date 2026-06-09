using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Security;

public static class KeycloakAuthenticationExtensions
{
    private const string ConfigurationSectionName = "Authentication:Keycloak";

    public static IServiceCollection AddKeycloakAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var keycloakOptions = configuration
            .GetSection(ConfigurationSectionName)
            .Get<KeycloakAuthenticationOptions>() ?? new KeycloakAuthenticationOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = keycloakOptions.Authority;
                if (!string.IsNullOrWhiteSpace(keycloakOptions.MetadataAddress))
                {
                    options.MetadataAddress = keycloakOptions.MetadataAddress;
                }

                options.Audience = keycloakOptions.Audience;
                options.RequireHttpsMetadata = keycloakOptions.RequireHttpsMetadata;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = keycloakOptions.ValidIssuer,
                    ValidIssuers = keycloakOptions.ValidIssuers,
                    ValidateAudience = keycloakOptions.ValidateAudience,
                    ValidAudience = keycloakOptions.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "preferred_username",
                    RoleClaimType = ClaimTypes.Role
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = MapKeycloakRolesAsync
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.UserOrAdmin, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(AuthorizationPolicies.AdminRole, AuthorizationPolicies.UserRole);
            });

            options.AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(AuthorizationPolicies.AdminRole);
            });
        });

        return services;
    }

    private static Task MapKeycloakRolesAsync(TokenValidatedContext context)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity)
        {
            return Task.CompletedTask;
        }

        foreach (var role in GetKeycloakRoles(context.Principal))
        {
            if (!identity.HasClaim(ClaimTypes.Role, role))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }
        }

        return Task.CompletedTask;
    }

    private static IEnumerable<string> GetKeycloakRoles(ClaimsPrincipal principal)
    {
        foreach (var claim in principal.FindAll("realm_access"))
        {
            foreach (var role in ReadRoles(claim.Value))
            {
                yield return role;
            }
        }

        foreach (var claim in principal.FindAll("resource_access"))
        {
            foreach (var role in ReadClientRoles(claim.Value))
            {
                yield return role;
            }
        }
    }

    private static IEnumerable<string> ReadRoles(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("roles", out var roles) || roles.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var role in roles.EnumerateArray())
        {
            if (role.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(role.GetString()))
            {
                yield return role.GetString()!;
            }
        }
    }

    private static IEnumerable<string> ReadClientRoles(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var client in document.RootElement.EnumerateObject())
        {
            if (!client.Value.TryGetProperty("roles", out var roles) || roles.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var role in roles.EnumerateArray())
            {
                if (role.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(role.GetString()))
                {
                    yield return role.GetString()!;
                }
            }
        }
    }
}

public sealed class KeycloakAuthenticationOptions
{
    public string Authority { get; set; } = "http://localhost:8080/realms/gateway-playground";

    public string? MetadataAddress { get; set; }

    public string Audience { get; set; } = "gateway-playground-api";

    public bool RequireHttpsMetadata { get; set; }

    public bool ValidateAudience { get; set; } = true;

    public string? ValidIssuer { get; set; }

    public string[]? ValidIssuers { get; set; }
}
