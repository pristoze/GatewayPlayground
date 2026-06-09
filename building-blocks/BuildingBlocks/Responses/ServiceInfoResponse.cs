namespace BuildingBlocks.Responses;

public sealed record ServiceInfoResponse(
    string Name,
    string Description,
    string Version,
    string Environment,
    DateTimeOffset ServerTimeUtc,
    IReadOnlyCollection<string> Capabilities);
