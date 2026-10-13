
namespace StargateAPI.Infrastructure.Identity
{
    /// <summary>
    /// A server-side login session. Its ID is carried inside the protected authentication cookie
    /// and checked on every request, so logout (revocation) takes effect on the next request even
    /// for a copied cookie. Never stores the cookie itself or any token material.
    /// </summary>
    public class AuthSession
    {
        // For EF Core materialization.
        private AuthSession()
        {
        }

        public Guid Id { get; private set; }

        public Guid UserId { get; private set; }

        public DateTimeOffset CreatedAtUtc { get; private set; }

        /// <summary>Fixed at sign-in; not extended by activity.</summary>
        public DateTimeOffset ExpiresAtUtc { get; private set; }

        public DateTimeOffset? RevokedAtUtc { get; private set; }

        public virtual ApplicationUser User { get; private set; } = null!;

        public static AuthSession Start(Guid userId, DateTimeOffset now, TimeSpan lifetime) => new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CreatedAtUtc = now.ToUniversalTime(),
            ExpiresAtUtc = now.ToUniversalTime() + lifetime
        };

        /// <summary>True when the session can still authenticate requests at <paramref name="now"/>.</summary>
        public bool IsActive(DateTimeOffset now) => RevokedAtUtc is null && now < ExpiresAtUtc;

        public void Revoke(DateTimeOffset now) => RevokedAtUtc ??= now.ToUniversalTime();
    }
}
