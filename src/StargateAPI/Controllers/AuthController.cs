using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StargateAPI.Domain;
using StargateAPI.Application.Logging;
using StargateAPI.Infrastructure.Identity;
using StargateAPI.Infrastructure.Persistence;
using StargateAPI.Domain.Results;
using StargateAPI.Security;

namespace StargateAPI.Controllers
{
    public sealed record LoginDto(string? UserName, string? Password);

    /// <summary>The signed-in account: identifiers, roles and the policies it satisfies. No secrets.</summary>
    public sealed record AuthMeDto(Guid Id, string UserName, IReadOnlyList<string> Roles, IReadOnlyList<string> Capabilities);

    /// <summary>
    /// Sign-in for the seeded Admin/Reader accounts. Clients first call GET /auth/antiforgery, then
    /// send the XSRF-TOKEN cookie value as the X-XSRF-TOKEN header on every POST/PUT, including
    /// login and logout. Login and logout issue a fresh token for the new identity.
    /// </summary>
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private const string XsrfCookieName = "XSRF-TOKEN";

        private readonly UserManager<ApplicationUser> _users;
        private readonly SignInManager<ApplicationUser> _signIn;
        private readonly StargateContext _context;
        private readonly IAntiforgery _antiforgery;
        private readonly IAuthorizationService _authorization;
        private readonly TimeProvider _timeProvider;
        private readonly AuthSettings _settings;
        private readonly OperationContext _operation;

        public AuthController(
            UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, StargateContext context,
            IAntiforgery antiforgery, IAuthorizationService authorization, TimeProvider timeProvider,
            IOptions<AuthSettings> settings, OperationContext operation)
        {
            _users = users;
            _signIn = signIn;
            _context = context;
            _antiforgery = antiforgery;
            _authorization = authorization;
            _timeProvider = timeProvider;
            _settings = settings.Value;
            _operation = operation;
        }

        /// <summary>Issues the antiforgery token (XSRF-TOKEN cookie). Not an authentication token.</summary>
        [AllowAnonymous]
        [HttpGet("antiforgery")]
        public IActionResult Antiforgery()
        {
            IssueXsrfToken();
            return NoContent();
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrEmpty(request.Password))
            {
                return this.ToProblem(AppError.Validation(new Dictionary<string, string[]>
                {
                    ["credentials"] = ["User name and password are required."]
                }));
            }

            // One generic answer for unknown users, wrong passwords, disabled and locked-out accounts.
            var user = await _users.FindByNameAsync(request.UserName.Trim());
            if (user is null || !user.IsEnabled)
            {
                return InvalidCredentials();
            }
            var check = await _signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!check.Succeeded)
            {
                return InvalidCredentials();
            }

            // Persist the session before issuing a cookie that refers to it. A storage failure
            // surfaces as an error (503 when busy), never as invalid credentials.
            var session = AuthSession.Start(user.Id, _timeProvider.GetUtcNow(), _settings.SessionLifetime);
            _context.AuthSessions.Add(session);
            await _context.SaveChangesAsync(cancellationToken);

            var principal = await _signIn.CreateUserPrincipalAsync(user);
            ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(AuthClaims.SessionId, session.Id.ToString()));
            await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal, new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = session.ExpiresAtUtc,
                AllowRefresh = false
            });

            // The antiforgery token is bound to the identity, so issue one for the signed-in user.
            HttpContext.User = principal;
            IssueXsrfToken();
            _operation.ActorSubject = user.Id.ToString();

            return Ok(await DescribeAsync(principal));
        }

        /// <summary>
        /// Revokes the current session in the database, then clears the cookie. If revocation
        /// cannot be saved the request fails and the cookie is kept: logout is never claimed when
        /// the session is still valid. Without a valid session this is a no-op.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken)
        {
            if (Guid.TryParse(User.FindFirstValue(AuthClaims.SessionId), out var sessionId)
                && Guid.TryParse(_users.GetUserId(User), out var userId))
            {
                var session = await _context.AuthSessions.SingleOrDefaultAsync(
                    s => s.Id == sessionId && s.UserId == userId, cancellationToken);
                if (session is not null)
                {
                    session.Revoke(_timeProvider.GetUtcNow());
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }

            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            IssueXsrfToken();
            return NoContent();
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me() => Ok(await DescribeAsync(User));

        private async Task<AuthMeDto> DescribeAsync(ClaimsPrincipal principal)
        {
            var capabilities = new List<string>();
            foreach (var policy in Policies.All)
            {
                if ((await _authorization.AuthorizeAsync(principal, policy)).Succeeded)
                {
                    capabilities.Add(policy);
                }
            }

            return new AuthMeDto(
                Guid.Parse(_users.GetUserId(principal)!),
                _users.GetUserName(principal)!,
                principal.FindAll(ClaimTypes.Role).Select(c => c.Value).Order().ToList(),
                capabilities);
        }

        private IActionResult InvalidCredentials()
        {
            _operation.ErrorCode = "invalid_credentials";
            var problem = ProblemDetailsFactory.CreateProblemDetails(
                HttpContext, StatusCodes.Status401Unauthorized, title: "The user name or password is incorrect.");
            problem.Extensions["code"] = "invalid_credentials";
            return new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                ContentTypes = { ControllerBaseExtensions.ProblemJson }
            };
        }

        /// <summary>
        /// Stores the antiforgery cookie token and exposes the request token in a script-readable
        /// XSRF-TOKEN cookie, which Angular echoes in the X-XSRF-TOKEN header (same-origin only).
        /// </summary>
        private void IssueXsrfToken()
        {
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
            Response.Cookies.Append(XsrfCookieName, tokens.RequestToken!, new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });
        }
    }
}
