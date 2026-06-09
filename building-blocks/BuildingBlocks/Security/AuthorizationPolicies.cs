namespace BuildingBlocks.Security;

public static class AuthorizationPolicies
{
    public const string AdminRole = "Admin";
    public const string UserRole = "User";
    public const string AdminOnly = "AdminOnly";
    public const string UserOrAdmin = "UserOrAdmin";
}
