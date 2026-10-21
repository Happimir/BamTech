using GreenDonut;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using MediatR;
using StargateAPI.Application.Dtos;
using StargateAPI.Application.Logging;
using StargateAPI.Application.Queries;
using StargateAPI.Domain;
using StargateAPI.Domain.Results;

using StargateAPI.Security;

namespace StargateAPI.GraphQL
{
    /// <summary>
    /// Reads. Each resolver sends the same MediatR request a controller action used to, so the
    /// use cases and their rules are unchanged; this class only shapes them as GraphQL fields.
    /// </summary>
    [QueryType]
    public static partial class PeopleQueries
    {
        /// <summary>
        /// People one page at a time (1-based page, size 1–100, default 25), in directory order,
        /// optionally filtered by a case-insensitive name prefix.
        /// </summary>
        [Authorize(Policy = Policies.PeopleRead)]
        public static async Task<PagedResponse<PersonDetailDto>> GetPeopleAsync(
            int? page, int? pageSize, string? name,
            IMediator mediator, OperationContext operation, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetPeople(page, pageSize, name), cancellationToken);
            return result.IsSuccess ? result.Value : throw UserErrors.ToException(result.Error, operation);
        }

        /// <summary>One person by stable ID, or null if there is none.</summary>
        [Authorize(Policy = Policies.PeopleRead)]
        public static async Task<PersonDetailDto?> GetPersonAsync(
            Guid id, IResolverContext context, IMediator mediator, IDutiesByPersonIdDataLoader duties,
            CancellationToken cancellationToken)
        {
            var result = await LoadPersonAsync(id, name: null, context, mediator, duties, cancellationToken);
            return result.IsSuccess ? result.Value : null;
        }

        /// <summary>
        /// One person by exact name (trimmed, case-insensitive, accents kept). Names are not
        /// unique: a name two people share is an AmbiguousNameError, never an arbitrary match.
        /// </summary>
        [Authorize(Policy = Policies.PeopleRead)]
        [Error<ValidationError>]
        [Error<NotFoundError>]
        [Error<AmbiguousNameError>]
        public static async Task<FieldResult<PersonDetailDto>> GetPersonByNameAsync(
            string name, IResolverContext context, IMediator mediator, IDutiesByPersonIdDataLoader duties,
            OperationContext operation, CancellationToken cancellationToken) =>
            (await LoadPersonAsync(id: null, name, context, mediator, duties, cancellationToken)).ToFieldResult(operation);

        /// <summary>The business date (UTC). Informational: the server re-checks dates on every write.</summary>
        [Authorize(Policy = Policies.PeopleRead)]
        public static BusinessMetadataDto GetBusinessCalendar(TimeProvider timeProvider) =>
            new(BusinessCalendar.Today(timeProvider), BusinessCalendar.TimeZone);

        /// <summary>
        /// When the duties are selected, the person and their history are read in one statement
        /// (one database snapshot) and the result primes the DataLoader, so the duties field
        /// returns that same snapshot instead of querying again.
        /// </summary>
        private static async Task<StargateAPI.Domain.Results.Result<PersonDetailDto>> LoadPersonAsync(
            Guid? id, string? name, IResolverContext context, IMediator mediator, IDutiesByPersonIdDataLoader duties,
            CancellationToken cancellationToken)
        {
            if (!context.IsSelected("duties"))
            {
                return await mediator.Send(new GetPerson(id, name), cancellationToken);
            }

            var snapshot = await mediator.Send(new GetPersonDuties(id, name), cancellationToken);
            if (!snapshot.IsSuccess)
            {
                return snapshot.Error;
            }
            duties.SetCacheEntry(snapshot.Value.Person.Id, snapshot.Value.Duties);
            return snapshot.Value.Person;
        }
    }
}
