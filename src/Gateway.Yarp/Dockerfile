# Build from the repository root:
# docker build -f src/Gateway.Yarp/Dockerfile -t gatewayplayground-gateway-yarp .
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Packages.props", "./"]
COPY ["building-blocks/BuildingBlocks/BuildingBlocks.csproj", "building-blocks/BuildingBlocks/"]
COPY ["src/Gateway.Yarp/Gateway.Yarp.csproj", "src/Gateway.Yarp/"]
RUN dotnet restore "src/Gateway.Yarp/Gateway.Yarp.csproj"
COPY . .
RUN dotnet publish "src/Gateway.Yarp/Gateway.Yarp.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Gateway.Yarp.dll"]
