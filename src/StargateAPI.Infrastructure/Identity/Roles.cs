namespace StargateAPI.Infrastructure.Identity
{
    /// <summary>Roles granted to accounts. Permissions are application-wide, not per Person.</summary>
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Reader = "Reader";
    }
}
