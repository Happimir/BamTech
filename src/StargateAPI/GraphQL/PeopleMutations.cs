using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using MediatR;
using StargateAPI.Application.Commands;
using StargateAPI.Application.Dtos;
using StargateAPI.Application.Logging;
using StargateAPI.Application.Queries;
using StargateAPI.Domain.Results;
using StargateAPI.Security;

namespace StargateAPI.GraphQL
{
    /// <summary>
    /// Selects a person by exactly one of a stable ID or an exact name (<c>@oneOf</c>). A name two
    /// people share is refused with an AmbiguousNameError and nothing is written.
    /// </summary>
    [OneOf]
    public sealed record PersonSelectorInput(Guid? Id, string? Name);

    /// <summary>
    /// Writes. Mutation conventions give each one an input object and a payload whose
    /// <c>errors</c> list holds the business errors it can return, as typed data. Fields that the
    /// schema marks required are checked by GraphQL before the resolver runs.
    /// </summary>
    [MutationType]
    public static partial class PeopleMutations
    {
        /// <summary>Always creates a new person, even when a namesake exists. Repeating it creates another.</summary>
        [Authorize(Policy = Policies.PeopleWrite)]
        [Error<ValidationError>]
        public static async Task<FieldResult<PersonDetailDto>> CreatePersonAsync(
            string name, string? firstName, string? middleName, string? lastName,
            IMediator mediator, OperationContext operation, CancellationToken cancellationToken) =>
            (await mediator.Send(new CreatePerson(name, firstName, middleName, lastName), cancellationToken))
                .ToFieldResult(operation);

        /// <summary>
        /// Replaces the name fields; omitted parts become null. Requires the version read from the
        /// person: a stale one is a ConcurrencyConflictError. The ID and duty history never change.
        /// </summary>
        [Authorize(Policy = Policies.PeopleWrite)]
        [Error<ValidationError>]
        [Error<NotFoundError>]
        [Error<ConcurrencyConflictError>]
        public static async Task<FieldResult<PersonDetailDto>> UpdatePersonAsync(
            Guid id, string name, string? firstName, string? middleName, string? lastName, Guid version,
            IMediator mediator, OperationContext operation, CancellationToken cancellationToken) =>
            (await mediator.Send(new UpdatePerson(id, name, firstName, middleName, lastName, version), cancellationToken))
                .ToFieldResult(operation);

        /// <summary>
        /// The assignment's "add/update a person by name". lookupName selects the person; name is
        /// the requested display name. With no match it creates only when both names are the same
        /// and no expectedVersion is sent, so a retried rename can never create a duplicate. A
        /// rename needs expectedVersion and clears the structured name parts.
        /// </summary>
        [Authorize(Policy = Policies.PeopleWrite)]
        [Error<ValidationError>]
        [Error<NotFoundError>]
        [Error<AmbiguousNameError>]
        [Error<ConcurrencyConflictError>]
        [Error<VersionRequiredError>]
        public static async Task<FieldResult<UpsertPersonByNamePayload>> UpsertPersonByNameAsync(
            string lookupName, string name, Guid? expectedVersion,
            IMediator mediator, OperationContext operation, CancellationToken cancellationToken) =>
            (await mediator.Send(new UpsertPersonByName(lookupName, name, expectedVersion), cancellationToken))
                .ToFieldResult(upsert => new UpsertPersonByNamePayload(upsert.Person, upsert.Created), operation);

        /// <summary>
        /// Appends a duty. The previous duty closes the day before; end and career dates are
        /// derived, never accepted. Send expectedVersion to refuse the write if the person changed.
        /// </summary>
        [Authorize(Policy = Policies.DutiesWrite)]
        [Error<ValidationError>]
        [Error<NotFoundError>]
        [Error<AmbiguousNameError>]
        [Error<ConcurrencyConflictError>]
        [Error<ChronologyConflictError>]
        [Error<FirstDutyRetirementError>]
        [Error<AlreadyRetiredError>]
        public static async Task<FieldResult<AssignDutyPayload>> AssignDutyAsync(
            PersonSelectorInput person, string rank, string dutyTitle, DateOnly dutyStartDate, Guid? expectedVersion,
            IMediator mediator, OperationContext operation, CancellationToken cancellationToken) =>
            (await mediator.Send(
                new AssignDuty(person.Id, person.Name, rank, dutyTitle, dutyStartDate, expectedVersion), cancellationToken))
                .ToFieldResult(created => new AssignDutyPayload(created.PersonId, created.Id), operation);
    }

    /// <summary>The person, and whether this call created them (false when found or renamed).</summary>
    public sealed record UpsertPersonByNamePayload(PersonDetailDto Person, bool Created);

    /// <summary>The new duty and the person it was added to, read after the change committed.</summary>
    public sealed record AssignDutyPayload([property: GraphQLIgnore] Guid PersonId, [property: GraphQLIgnore] Guid DutyId)
    {
        public async Task<DutyDto> GetDutyAsync(IMediator mediator, CancellationToken cancellationToken) =>
            Required(await mediator.Send(new GetPersonDuty(PersonId, DutyId), cancellationToken));

        /// <summary>The person as it is now, including the new version for the next write.</summary>
        public async Task<PersonDetailDto> GetPersonAsync(IMediator mediator, CancellationToken cancellationToken) =>
            Required(await mediator.Send(new GetPerson(Id: PersonId), cancellationToken));

        // The duty was committed by this request; a missing row is a fault, not a user error.
        private static T Required<T>(Result<T> result) =>
            result.IsSuccess ? result.Value : throw new InvalidOperationException("The assigned duty could not be read back.");
    }
}
