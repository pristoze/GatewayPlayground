namespace BuildingBlocks.Responses;

public sealed record ApiResponse<T>(
    bool Success,
    T? Data,
    string? Message,
    string? CorrelationId)
{
    public static ApiResponse<T> Ok(T data, string? correlationId = null, string? message = null)
    {
        return new ApiResponse<T>(true, data, message, correlationId);
    }
}
