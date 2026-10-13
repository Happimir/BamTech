using StargateAPI.Business.Data;
using StargateAPI.Business.Results;

namespace StargateAPI.Business.Domain
{
    /// <summary>
    /// Validated person name fields. Name is the required display name; the optional parts are
    /// explicitly supplied metadata, never derived by splitting Name. Whitespace-only parts become null.
    /// </summary>
    public sealed record PersonNameInput(string Name, string? FirstName, string? MiddleName, string? LastName)
    {
        public static Result<PersonNameInput> Create(string? name, string? firstName, string? middleName, string? lastName)
        {
            var errors = new Dictionary<string, string[]>();
            if (!NameNormalizer.TryNormalize(name, out _, out var nameError))
            {
                errors["name"] = [nameError!];
            }

            var first = Part(errors, "firstName", firstName);
            var middle = Part(errors, "middleName", middleName);
            var last = Part(errors, "lastName", lastName);

            if (errors.Count > 0)
            {
                return AppError.Validation(errors);
            }

            return new PersonNameInput(name!.Trim(), first, middle, last);
        }

        private static string? Part(Dictionary<string, string[]> errors, string field, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if (trimmed.Length > Person.MaxNamePartLength)
            {
                errors[field] = [$"Must be at most {Person.MaxNamePartLength} characters."];
            }
            return trimmed;
        }
    }
}
