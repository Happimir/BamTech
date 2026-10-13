using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using StargateAPI.Business.Data;

namespace StargateAPI.Security
{
    public static class SecurityServiceCollectionExtensions
    {
        public const string AuthCookieName = "__Host-acts";
        public const string AntiforgeryCookieName = "__Host-acts-af";
        public const string XsrfHeaderName = "X-XSRF-TOKEN";

        /// <summary>
        /// ASP.NET Core Identity with framework password hashing and lockout, cookie authentication
        /// backed by revocable database sessions, role-based policies (authentication required by
        /// default), and antiforgery protection for state-changing requests.
        /// </summary>
        public static IServiceCollection AddActsSecurity(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<AuthSettings>(configuration.GetSection("Auth"));

            services.AddIdentityCore<ApplicationUser>(options =>
                {
                    options.Lockout.AllowedForNewUsers = true;
                    options.Lockout.MaxFailedAccessAttempts = 5;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                })
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<StargateContext>()
                .AddSignInManager();

            services.AddAuthentication(IdentityConstants.ApplicationScheme)
                .AddCookie(IdentityConstants.ApplicationScheme, options =>
                {
                    // __Host- prefix: Secure, Path=/, no Domain, so the cookie stays on this origin.
                    options.Cookie.Name = AuthCookieName;
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.Cookie.Path = "/";
                    // The expiry is set per sign-in from the session; activity never extends it.
                    options.SlidingExpiration = false;

                    options.Events.OnValidatePrincipal = SessionValidation.ValidateAsync;
                    // An API answers 401/403; it does not redirect to a login page.
                    options.Events.OnRedirectToLogin = context => ProblemResponses.WriteAsync(
                        context.HttpContext, StatusCodes.Status401Unauthorized, "unauthenticated", "Sign in to continue.");
                    options.Events.OnRedirectToAccessDenied = context => ProblemResponses.WriteAsync(
                        context.HttpContext, StatusCodes.Status403Forbidden, "forbidden", "Your account is not allowed to do this.");
                });

            services.AddAuthorizationBuilder()
                // Every endpoint requires a signed-in user unless it explicitly allows anonymous access.
                .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
                .AddPolicy(Policies.PeopleRead, policy => policy.RequireRole(Roles.Admin, Roles.Reader))
                .AddPolicy(Policies.PeopleWrite, policy => policy.RequireRole(Roles.Admin))
                .AddPolicy(Policies.DutiesWrite, policy => policy.RequireRole(Roles.Admin));

            services.AddAntiforgery(options =>
            {
                options.HeaderName = XsrfHeaderName;
                options.Cookie.Name = AntiforgeryCookieName;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
            });

            return services;
        }
    }
}
