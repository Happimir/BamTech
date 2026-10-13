using MediatR;
using StargateAPI.Application.Abstractions;
using StargateAPI.Domain;
using StargateAPI.Application.Logging;
using StargateAPI.Application.Dtos;
using StargateAPI.Domain.Results;

namespace StargateAPI.Application.Commands
{
    /// <summary>
    /// Creates a new person with a new stable identity. Display names are not unique, so this
    /// always creates, even when a namesake exists; repeating it creates another person.
    /// </summary>
    public sealed record CreatePerson(
        string? Name,
        string? FirstName = null,
        string? MiddleName = null,
        string? LastName = null) : IRequest<Result<PersonDetailDto>>;

    public class CreatePersonHandler : IRequestHandler<CreatePerson, Result<PersonDetailDto>>
    {
        private readonly IStargateDbContext _context;
        private readonly TimeProvider _timeProvider;
        private readonly OperationContext _operation;

        public CreatePersonHandler(IStargateDbContext context, TimeProvider timeProvider, OperationContext operation)
        {
            _context = context;
            _timeProvider = timeProvider;
            _operation = operation;
        }

        public async Task<Result<PersonDetailDto>> Handle(CreatePerson request, CancellationToken cancellationToken)
        {
            var input = PersonNameInput.Create(request.Name, request.FirstName, request.MiddleName, request.LastName);
            if (!input.IsSuccess)
            {
                return input.Error;
            }

            var names = input.Value;
            var person = Person.Create(names.Name, names.FirstName, names.MiddleName, names.LastName);
            _context.People.Add(person);

            // One SaveChanges is one transaction: the person and its audit commit together.
            _operation.PersonId = person.Id;
            _context.ProcessLogs.Add(ProcessLog.MutationAudit(
                _operation, "CreatePerson", _timeProvider.GetUtcNow(), person.Id, dutyId: null,
                "Person created.",
                LogContent.Changes(new { HasNameParts = names.FirstName is not null || names.MiddleName is not null || names.LastName is not null })));
            await _context.SaveChangesAsync(cancellationToken);
            _operation.MutationCommitted = true;

            return PersonDtoMapper.ToDetail(person, detail: null);
        }
    }
}
