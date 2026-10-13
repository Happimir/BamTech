using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Business.Domain;
using StargateAPI.Business.Results;

namespace StargateAPI.Business.Queries
{
    /// <summary>Name-based compatibility lookups. Names are not identities: never pick an arbitrary match.</summary>
    public static class PersonLookup
    {
        /// <summary>
        /// Resolves an exact normalized name to one person ID: zero matches is not_found and more
        /// than one is ambiguous_name. Call inside the mutation transaction when used by a write.
        /// </summary>
        public static async Task<Result<Guid>> ResolveIdByNameAsync(
            StargateContext context, string? name, CancellationToken cancellationToken)
        {
            if (!NameNormalizer.TryNormalize(name, out var key, out var error))
            {
                return AppError.Validation("name", error!);
            }

            var ids = await context.People
                .AsNoTracking()
                .Where(p => p.NormalizedName == key)
                .Select(p => p.Id)
                .Take(2)
                .ToListAsync(cancellationToken);

            return ids.Count switch
            {
                0 => AppError.NotFound("No person has this name."),
                1 => ids[0],
                _ => AppError.AmbiguousName()
            };
        }
    }
}
