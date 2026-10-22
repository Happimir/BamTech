using GreenDonut;
using HotChocolate;
using HotChocolate.Types;
using MediatR;
using StargateAPI.Application.Dtos;
using StargateAPI.Application.Queries;

namespace StargateAPI.GraphQL
{
    /// <summary>
    /// GraphQL names for the application's DTOs. The DTOs stay framework-free in the Application
    /// layer; these classes only name them and add the fields that are resolved separately.
    /// </summary>
    [ObjectType<PersonDetailDto>]
    public static partial class PersonNode
    {
        static partial void Configure(IObjectTypeDescriptor<PersonDetailDto> descriptor) =>
            descriptor.Name("Person")
                .Description("A person with a stable ID. Names are not unique; namesakes have separate IDs and histories.");

        /// <summary>
        /// The duty history, newest first. Batched across people by the DataLoader; when a
        /// single-person read already loaded the history in its snapshot, that copy is used.
        /// </summary>
        public static async Task<IReadOnlyList<DutyDto>> GetDutiesAsync(
            [Parent] PersonDetailDto person, IDutiesByPersonIdDataLoader duties, CancellationToken cancellationToken) =>
            await duties.LoadRequiredAsync(person.Id, cancellationToken);
    }

    [ObjectType<AstronautSummaryDto>]
    public static partial class AstronautSummaryNode
    {
        static partial void Configure(IObjectTypeDescriptor<AstronautSummaryDto> descriptor) =>
            descriptor.Name("AstronautSummary")
                .Description("The current assignment; null on a person with no assignments (rule 2).");
    }

    [ObjectType<DutyDto>]
    public static partial class DutyNode
    {
        static partial void Configure(IObjectTypeDescriptor<DutyDto> descriptor) =>
            descriptor.Name("Duty")
                .Description("One assignment. The current duty has no end date (rule 4).");
    }

    [ObjectType<BusinessMetadataDto>]
    public static partial class BusinessCalendarNode
    {
        static partial void Configure(IObjectTypeDescriptor<BusinessMetadataDto> descriptor) =>
            descriptor.Name("BusinessCalendar")
                .Description("The server's \"today\" for date rules (the latest allowed duty start date), and its zone.");
    }

    [ObjectType<PagedResponse<PersonDetailDto>>]
    public static partial class PersonPageNode
    {
        static partial void Configure(IObjectTypeDescriptor<PagedResponse<PersonDetailDto>> descriptor) =>
            descriptor.Name("PersonPage")
                .Description("One page of the directory. A page beyond the last has no items; totalPages is 0 when nothing matches.");
    }

    public static class DutyDataLoaders
    {
        /// <summary>Duty histories for every person requested in one execution step, in one query.</summary>
        [DataLoader]
        public static Task<IReadOnlyDictionary<Guid, IReadOnlyList<DutyDto>>> GetDutiesByPersonIdAsync(
            IReadOnlyList<Guid> personIds, IMediator mediator, CancellationToken cancellationToken) =>
            mediator.Send(new GetDutiesByPersonIds(personIds), cancellationToken);
    }
}
