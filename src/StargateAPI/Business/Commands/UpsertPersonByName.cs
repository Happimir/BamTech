using MediatR;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Business.Domain;
using StargateAPI.Business.Dtos;
using StargateAPI.Business.Results;

namespace StargateAPI.Business.Commands
{
    /// <summary>
    /// The required "add/update a person by name", as a compatibility adapter over stable IDs.
    /// LookupName selects the person; Name is the requested display name.
    /// <list type="bullet">
    /// <item>Several matches: 409 ambiguous_name. Use the ID route.</item>
    /// <item>No match: creates only when Name is the same name as LookupName and no
    /// ExpectedVersion is sent; otherwise 404. So retrying "Alice" to "Alicia" after it succeeded
    /// can never create a second Alicia.</item>
    /// <item>One match, same display name: a no-op (a supplied ExpectedVersion is still checked).</item>
    /// <item>One match, new display name: ExpectedVersion is required and must be current.</item>
    /// </list>
    /// </summary>
    public sealed record UpsertPersonByName(string? LookupName, string? Name, Guid? ExpectedVersion)
        : IRequest<Result<UpsertPersonResult>>;

    public sealed record UpsertPersonResult(PersonDetailDto Person, bool Created);

    public class UpsertPersonByNameHandler : IRequestHandler<UpsertPersonByName, Result<UpsertPersonResult>>
    {
        private readonly StargateContext _context;

        public UpsertPersonByNameHandler(StargateContext context)
        {
            _context = context;
        }

        public async Task<Result<UpsertPersonResult>> Handle(UpsertPersonByName request, CancellationToken cancellationToken)
        {
            var errors = new Dictionary<string, string[]>();
            if (!NameNormalizer.TryNormalize(request.LookupName, out var lookupKey, out var lookupError))
            {
                errors["lookupName"] = [lookupError!];
            }
            if (!NameNormalizer.TryNormalize(request.Name, out var requestedKey, out var nameError))
            {
                errors["name"] = [nameError!];
            }
            if (errors.Count > 0)
            {
                return AppError.Validation(errors);
            }
            var requestedName = request.Name!.Trim();

            // IMMEDIATE transaction before resolving: two concurrent upserts of a missing name
            // serialize here, so the second sees the first's insert instead of creating a duplicate.
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var matches = await _context.People
                .Where(p => p.NormalizedName == lookupKey)
                .Take(2)
                .ToListAsync(cancellationToken);

            if (matches.Count > 1)
            {
                return AppError.AmbiguousName();
            }

            if (matches.Count == 0)
            {
                if (lookupKey != requestedKey || request.ExpectedVersion is not null)
                {
                    return AppError.NotFound(
                        "No person has this name. A rename needs an existing person; to create one, use the same name in the route and body.");
                }

                var created = Person.Create(requestedName);
                _context.People.Add(created);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new UpsertPersonResult(PersonDtoMapper.ToDetail(created, detail: null), Created: true);
            }

            var person = matches[0];
            var nameChanges = person.Name != requestedName;

            if (request.ExpectedVersion is { } expected && expected != person.Version)
            {
                return AppError.Conflict(ErrorCodes.ConcurrencyConflict,
                    "This person was changed by another request. Reload and review before trying again.");
            }
            if (nameChanges && request.ExpectedVersion is null)
            {
                return AppError.Conflict(ErrorCodes.VersionRequired,
                    "Changing a name requires expectedVersion. Read the person first and send its version.");
            }

            if (person.ChangeDisplayName(requestedName))
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            var detail = await _context.AstronautDetails.AsNoTracking()
                .SingleOrDefaultAsync(d => d.PersonId == person.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new UpsertPersonResult(PersonDtoMapper.ToDetail(person, detail), Created: false);
        }
    }
}
