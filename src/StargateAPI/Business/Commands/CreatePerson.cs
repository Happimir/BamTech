using MediatR;
using StargateAPI.Business.Data;
using StargateAPI.Business.Domain;
using StargateAPI.Business.Dtos;
using StargateAPI.Business.Results;

namespace StargateAPI.Business.Commands
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
        private readonly StargateContext _context;

        public CreatePersonHandler(StargateContext context)
        {
            _context = context;
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
            await _context.SaveChangesAsync(cancellationToken);

            return PersonDtoMapper.ToDetail(person, detail: null);
        }
    }
}
