namespace StargateAPI.Security
{
    /// <summary>Roles granted to accounts. Permissions are application-wide, not per Person.</summary>
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Reader = "Reader";
    }

    /// <summary>
    /// Authorization policies applied to every route, legacy and ID-based alike. Admin holds all
    /// three; Reader holds People.Read only.
    /// </summary>
    public static class Policies
    {
        public const string PeopleRead = "People.Read";
        public const string PeopleWrite = "People.Write";
        public const string DutiesWrite = "Duties.Write";

        public static readonly IReadOnlyList<string> All = [PeopleRead, PeopleWrite, DutiesWrite];
    }

    /// <summary>Claim types issued by this application.</summary>
    public static class AuthClaims
    {
        /// <summary>The server-side AuthSession ID carried inside the protected cookie.</summary>
        public const string SessionId = "acts:sid";
    }

    /// <summary>Bound from the "Auth" configuration section.</summary>
    public sealed class AuthSettings
    {
        /// <summary>Fixed session lifetime; activity does not extend it.</summary>
        public int SessionLifetimeMinutes { get; set; } = 480;

        public TimeSpan SessionLifetime => TimeSpan.FromMinutes(SessionLifetimeMinutes);
    }
}
