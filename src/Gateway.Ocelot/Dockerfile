# Build from the repository root:
# docker build -f src/Gateway.Ocelot/Dockerfile -t gatewayplayground-gateway-ocelot .
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Packages.props", "./"]
COPY ["building-blocks/BuildingBlocks/BuildingBlocks.csproj", "building-blocks/BuildingBlocks/"]
COPY ["src/Gateway.Ocelot/Gateway.Ocelot.csproj", "src/Gateway.Ocelot/"]
RUN dotnet restore "src/Gateway.Ocelot/Gateway.Ocelot.csproj"
COPY . .
RUN dotnet publish "src/Gateway.Ocelot/Gateway.Ocelot.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Gateway.Ocelot.dll"]
