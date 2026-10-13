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
            var errors = new Dictionary<string, string[]>();
            if (!NameNormalizer.TryNormalize(request.Name, out _, out var nameError))
            {
                errors["name"] = [nameError!];
            }
            AddPartError(errors, "firstName", request.FirstName);
            AddPartError(errors, "middleName", request.MiddleName);
            AddPartError(errors, "lastName", request.LastName);
            if (errors.Count > 0)
            {
                return AppError.Validation(errors);
            }

            var person = Person.Create(request.Name!, request.FirstName, request.MiddleName, request.LastName);
            _context.People.Add(person);
            await _context.SaveChangesAsync(cancellationToken);

            return new PersonDetailDto(
                person.Id, person.Name, person.Version, null, person.FirstName, person.MiddleName, person.LastName);
        }

        private static void AddPartError(Dictionary<string, string[]> errors, string field, string? value)
        {
            if (value is not null && value.Trim().Length > Person.MaxNamePartLength)
            {
                errors[field] = [$"Must be at most {Person.MaxNamePartLength} characters."];
            }
        }
    }
}
