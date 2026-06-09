# GatewayPlayground

Gateway comparison playground for three runtime-selectable architectures:

| Mode | Flow |
| --- | --- |
| Mode A | Monolith -> Services |
| Mode B | Monolith -> Gateway.Yarp -> Services |
| Mode C | Monolith -> Gateway.Ocelot -> Services |

The active mode is selected by launch profile or environment variables. No source code changes are required.

## Gateway Routes

Both gateways expose the same service route shape:

| Gateway Path | Downstream Service |
| --- | --- |
| `/api/search/*` | `SearchService` |
| `/api/mail/*` | `MailService` |
| `/api/deduplication/*` | `DeduplicationService` |
| `/api/users/*` | `UserService` |

All API routes require a valid Keycloak JWT with either the `User` or `Admin` role. Admin-only endpoints use `/admin` and require the `Admin` role.

## Keycloak

Docker Compose runs Keycloak `26.6.3` from `quay.io/keycloak/keycloak` and imports [realm-export.json](keycloak/realm-export.json).

Realm and users:

| Item | Value |
| --- | --- |
| Realm | `gateway-playground` |
| Client | `gateway-playground-api` |
| Admin console | `http://localhost:8080` |
| Bootstrap admin | `admin` / `admin` |
| API admin user | `admin` / `admin` |
| API test user | `testuser` / `testuser` |

Roles:

| User | Roles |
| --- | --- |
| `admin` | `Admin`, `User` |
| `testuser` | `User` |

Get a `User` token:

```powershell
$tokenResponse = Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:8080/realms/gateway-playground/protocol/openid-connect/token" `
  -ContentType "application/x-www-form-urlencoded" `
  -Body @{
    client_id = "gateway-playground-api"
    grant_type = "password"
    username = "testuser"
    password = "testuser"
  }

$token = $tokenResponse.access_token
```

Get an `Admin` token by changing `username` and `password` to `admin`.

Swagger supports JWT bearer authentication. Open a Swagger UI, click `Authorize`, and paste the access token.

## Docker Compose Profiles

Run exactly one profile at a time because all modes publish the monolith on `http://localhost:5256`.

```powershell
docker compose --profile mode-a up --build
docker compose --profile mode-b up --build
docker compose --profile mode-c up --build
```

Stop the active profile:

```powershell
docker compose --profile mode-c down
```

Profile contents:

| Profile | Services |
| --- | --- |
| `mode-a` | Keycloak + Monolith + Search/Mail/Deduplication/User services |
| `mode-b` | Keycloak + Monolith + Gateway.Yarp + services |
| `mode-c` | Keycloak + Monolith + Gateway.Ocelot + services |

## Local Launch Profiles

Start Keycloak first:

```powershell
docker compose --profile mode-a up -d keycloak
```

Start the downstream service projects with their `http` launch profiles:

```powershell
dotnet run --project src/SearchService --launch-profile http
dotnet run --project src/MailService --launch-profile http
dotnet run --project src/DeduplicationService --launch-profile http
dotnet run --project src/UserService --launch-profile http
```

Then select one monolith mode:

```powershell
dotnet run --project src/Monolith --launch-profile mode-a
```

For Mode B, also start YARP:

```powershell
dotnet run --project src/Gateway.Yarp --launch-profile mode-b
dotnet run --project src/Monolith --launch-profile mode-b
```

For Mode C, also start Ocelot:

```powershell
dotnet run --project src/Gateway.Ocelot --launch-profile mode-c
dotnet run --project src/Monolith --launch-profile mode-c
```

## Operational Endpoints

| Component | URL |
| --- | --- |
| Monolith topology | `http://localhost:5256/api/monolith/architecture` |
| Monolith downstream probe | `http://localhost:5256/api/monolith/probe` |
| Monolith health | `http://localhost:5256/health` |
| YARP gateway status | `http://localhost:5205/api/gateway` |
| YARP Swagger UI | `http://localhost:5205/swagger` |
| Ocelot gateway status | `http://localhost:5210/api/gateway` |
| Ocelot Swagger UI | `http://localhost:5210/swagger` |
| Ocelot health | `http://localhost:5210/health` |

Protected endpoint examples:

| Scenario | Example |
| --- | --- |
| No token -> `401` | `GET http://localhost:5256/api/monolith` |
| Invalid token -> `401` | `GET http://localhost:5256/api/monolith` with `Authorization: Bearer invalid` |
| Valid token -> `200` | `GET http://localhost:5256/api/monolith` with `testuser` token |
| Missing role -> `403` | `GET http://localhost:5256/api/monolith/admin` with `testuser` token |
| Admin role -> `200` | `GET http://localhost:5256/api/monolith/admin` with `admin` token |

PowerShell example:

```powershell
Invoke-RestMethod `
  -Uri "http://localhost:5256/api/monolith" `
  -Headers @{ Authorization = "Bearer $token" }
```

## Configuration

Monolith mode selection is controlled by:

```text
Architecture__Mode
Architecture__Flow
DownstreamServices__{ServiceName}__BaseAddress
```

`Gateway.Ocelot` uses `src/Gateway.Ocelot/ocelot.json` for routing and Swagger aggregation. Docker Compose overrides Ocelot downstream hosts from `localhost` to Compose service names with environment variables.

Keycloak validation is controlled by:

```text
Authentication__Keycloak__Authority
Authentication__Keycloak__MetadataAddress
Authentication__Keycloak__ValidIssuers__0
Authentication__Keycloak__Audience
```

Local appsettings use `http://localhost:8080/realms/gateway-playground`. Docker Compose overrides metadata discovery to `http://keycloak:8080/...` while accepting tokens issued from `http://localhost:8080/...`.

Correlation IDs use the shared `X-Correlation-ID` header. The gateways and monolith preserve incoming values and generate one when the header is absent.
