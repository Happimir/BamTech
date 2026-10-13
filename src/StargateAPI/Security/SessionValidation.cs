using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StargateAPI.Business.Data;

namespace StargateAPI.Security
{
    /// <summary>
    /// Runs on every authenticated request (cookie OnValidatePrincipal). The cookie alone is not
    /// trusted: its session must exist, belong to the claimed user, be unrevoked and unexpired, and
    /// the user must still exist, be enabled and have the same security stamp. This performs
    /// Identity's security-stamp check on every request rather than replacing it, which is what
    /// makes logout revoke a copied cookie on its next use. Results are deliberately not cached,
    /// and a storage failure throws (fails closed) rather than letting the request through.
    /// </summary>
    public static class SessionValidation
    {
        public static async Task ValidateAsync(CookieValidatePrincipalContext context)
        {
            var services = context.HttpContext.RequestServices;
            var claimTypes = services.GetRequiredService<IOptions<IdentityOptions>>().Value.ClaimsIdentity;
            var principal = context.Principal;

            if (!Guid.TryParse(principal?.FindFirstValue(AuthClaims.SessionId), out var sessionId)
                || !Guid.TryParse(principal?.FindFirstValue(claimTypes.UserIdClaimType), out var userId)
                || principal?.FindFirstValue(claimTypes.SecurityStampClaimType) is not { } stamp)
            {
                await RejectAsync(context);
                return;
            }

            var session = await services.GetRequiredService<StargateContext>().AuthSessions
                .AsNoTracking()
                .Where(s => s.Id == sessionId)
                .Select(s => new { s.UserId, s.ExpiresAtUtc, s.RevokedAtUtc, s.User.IsEnabled, s.User.SecurityStamp })
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

            var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
            if (session is null
                || session.UserId != userId
                || session.RevokedAtUtc is not null
                || now >= session.ExpiresAtUtc
                || !session.IsEnabled
                || session.SecurityStamp != stamp)
            {
                await RejectAsync(context);
            }
        }

        private static async Task RejectAsync(CookieValidatePrincipalContext context)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    }
}
