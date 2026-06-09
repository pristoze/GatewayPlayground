# Build from the repository root:
# docker build -f src/DeduplicationService/Dockerfile -t gatewayplayground-deduplicationservice .
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Packages.props", "./"]
COPY ["building-blocks/BuildingBlocks/BuildingBlocks.csproj", "building-blocks/BuildingBlocks/"]
COPY ["src/DeduplicationService/DeduplicationService.csproj", "src/DeduplicationService/"]
RUN dotnet restore "src/DeduplicationService/DeduplicationService.csproj"
COPY . .
RUN dotnet publish "src/DeduplicationService/DeduplicationService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DeduplicationService.dll"]
