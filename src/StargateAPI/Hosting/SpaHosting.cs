using Microsoft.Extensions.FileProviders;
using StargateAPI.Controllers;

namespace StargateAPI.Hosting
{
    /// <summary>
    /// Optionally serves the built Angular app (npm run build) from the API, so a single
    /// `dotnet run` hosts both on one origin. The dev server with its proxy stays the primary
    /// development flow; when the build output is absent, the API behaves exactly as without it.
    /// </summary>
    public static class SpaHosting
    {
        public const string RootPathSetting = "Spa:RootPath";

        // Deep links (/people/{id}) fall back to index.html; paths owned by the API never do, so
        // their 404s stay problem responses. Catch-all values carry no leading slash; the regex
        // constraint is case-insensitive. It rejects an absent value, so the root is mapped too.
        private static readonly string[] FallbackPatterns =
            ["/", "{*path:regex(^(?!(api|auth|swagger|person|astronautduty)(/|$)).*$):nonfile}"];

        /// <summary>The build output, or null when hosting is off (setting empty or no index.html).</summary>
        public static IFileProvider? ResolveFiles(IConfiguration configuration, IWebHostEnvironment environment)
        {
            var rootPath = configuration[RootPathSetting];
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return null;
            }
            var fullPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, rootPath));
            return File.Exists(Path.Combine(fullPath, "index.html")) ? new PhysicalFileProvider(fullPath) : null;
        }

        /// <summary>
        /// Registered first in the pipeline: hashed bundles are public, so they skip the outcome
        /// log, exception handling and the authenticated-by-default fallback policy.
        /// </summary>
        public static void UseSpaStaticFiles(this WebApplication app, IFileProvider files) =>
            app.UseStaticFiles(new StaticFileOptions { FileProvider = files });

        /// <summary>
        /// The app shell is anonymous (the Angular guards and the API decide access) and is not a
        /// business operation, so it is not logged. no-cache keeps a rebuilt shell from going stale.
        /// </summary>
        public static void MapSpaFallback(this WebApplication app, IFileProvider files)
        {
            var shell = new StaticFileOptions
            {
                FileProvider = files,
                OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache"
            };
            foreach (var pattern in FallbackPatterns)
            {
                app.MapFallbackToFile(pattern, "index.html", shell)
                    .AllowAnonymous()
                    .WithMetadata(SkipRequestOutcomeLog.Instance);
            }
        }
    }
}
