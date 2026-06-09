namespace BuildingBlocks.Security;

public static class GatewayClaimHeaders
{
    public const string Authenticated = "X-Gateway-Authenticated";
    public const string UserId = "X-Gateway-User-Id";
    public const string UserName = "X-Gateway-User-Name";
    public const string Roles = "X-Gateway-Roles";
    public const string Claims = "X-Gateway-Claims";

    public static readonly string[] All =
    [
        Authenticated,
        UserId,
        UserName,
        Roles,
        Claims
    ];
}
