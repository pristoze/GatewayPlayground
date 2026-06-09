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

Authentication is intentionally not implemented yet.

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
| `mode-a` | Monolith + Search/Mail/Deduplication/User services |
| `mode-b` | Monolith + Gateway.Yarp + services |
| `mode-c` | Monolith + Gateway.Ocelot + services |

## Local Launch Profiles

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

## Configuration

Monolith mode selection is controlled by:

```text
Architecture__Mode
Architecture__Flow
DownstreamServices__{ServiceName}__BaseAddress
```

`Gateway.Ocelot` uses `src/Gateway.Ocelot/ocelot.json` for routing and Swagger aggregation. Docker Compose overrides Ocelot downstream hosts from `localhost` to Compose service names with environment variables.

Correlation IDs use the shared `X-Correlation-ID` header. The gateways and monolith preserve incoming values and generate one when the header is absent.
