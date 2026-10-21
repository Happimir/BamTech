using HotChocolate;
using HotChocolate.Types;
using StargateAPI.Application.Logging;
using StargateAPI.Domain.Results;
using ErrorCodes = StargateAPI.Domain.Results.ErrorCodes;

namespace StargateAPI.GraphQL
{
    /// <summary>
    /// The interface every business error implements. Clients branch on the concrete type
    /// (<c>__typename</c>) or the stable <c>code</c>, never on the message.
    /// </summary>
    [GraphQLName("Error")]
    public interface IUserError
    {
        /// <summary>Safe to show to a user as written.</summary>
        string Message { get; }

        /// <summary>The stable error code, as stored in the process log.</summary>
        string Code { get; }
    }

    /// <summary>One invalid input field and why. Field names match the input's camelCase names.</summary>
    public sealed record InvalidField(string Field, IReadOnlyList<string> Messages);

    public sealed record ValidationError(string Message, IReadOnlyList<InvalidField> Fields) : IUserError
    {
        public string Code => ErrorCodes.Validation;
    }

    public sealed record NotFoundError(string Message) : IUserError
    {
        public string Code => ErrorCodes.NotFound;
    }

    /// <summary>More than one person has the name; select the person by ID instead.</summary>
    public sealed record AmbiguousNameError(string Message) : IUserError
    {
        public string Code => ErrorCodes.AmbiguousName;
    }

    /// <summary>The person changed after the client read it; reload and review.</summary>
    public sealed record ConcurrencyConflictError(string Message) : IUserError
    {
        public string Code => ErrorCodes.ConcurrencyConflict;
    }

    /// <summary>A rename by name needs the person's current version.</summary>
    public sealed record VersionRequiredError(string Message) : IUserError
    {
        public string Code => ErrorCodes.VersionRequired;
    }

    /// <summary>The start date is not after the person's current duty (rule 5).</summary>
    public sealed record ChronologyConflictError(string Message) : IUserError
    {
        public string Code => ErrorCodes.ChronologyConflict;
    }

    /// <summary>Retirement cannot be a person's first duty.</summary>
    public sealed record FirstDutyRetirementError(string Message) : IUserError
    {
        public string Code => ErrorCodes.FirstDutyRetirement;
    }

    /// <summary>The person is already retired.</summary>
    public sealed record AlreadyRetiredError(string Message) : IUserError
    {
        public string Code => ErrorCodes.AlreadyRetired;
    }

    /// <summary>Maps application results to GraphQL: the value, or the matching error type as data.</summary>
    public static class UserErrors
    {
        /// <summary>
        /// Converts a failed result into its error object and records the code for the request's
        /// process log row, which is otherwise a success: business errors are data, so HTTP says 200.
        /// </summary>
        public static object From(AppError error, OperationContext operation)
        {
            operation.ErrorCode = error.Code;
            return error.Code switch
            {
                ErrorCodes.Validation => new ValidationError(error.Message,
                    (error.FieldErrors ?? new Dictionary<string, string[]>())
                        .Select(field => new InvalidField(field.Key, field.Value)).ToList()),
                ErrorCodes.NotFound => new NotFoundError(error.Message),
                ErrorCodes.AmbiguousName => new AmbiguousNameError(error.Message),
                ErrorCodes.ConcurrencyConflict => new ConcurrencyConflictError(error.Message),
                ErrorCodes.VersionRequired => new VersionRequiredError(error.Message),
                ErrorCodes.ChronologyConflict => new ChronologyConflictError(error.Message),
                ErrorCodes.FirstDutyRetirement => new FirstDutyRetirementError(error.Message),
                ErrorCodes.AlreadyRetired => new AlreadyRetiredError(error.Message),
                _ => throw new InvalidOperationException($"No GraphQL error type is defined for '{error.Code}'.")
            };
        }

        public static FieldResult<TOut> ToFieldResult<TIn, TOut>(
            this Result<TIn> result, Func<TIn, TOut> map, OperationContext operation) =>
            result.IsSuccess ? map(result.Value) : new FieldResult<TOut>(From(result.Error, operation));

        public static FieldResult<T> ToFieldResult<T>(this Result<T> result, OperationContext operation) =>
            result.ToFieldResult(value => value, operation);

        /// <summary>
        /// For query arguments the schema cannot express (a page size range): a request error with
        /// the stable code and per-field messages in its extensions, rather than a typed result.
        /// </summary>
        public static GraphQLException ToException(AppError error, OperationContext operation)
        {
            operation.ErrorCode = error.Code;
            var builder = ErrorBuilder.New().SetMessage(error.Message).SetCode(error.Code);
            if (error.FieldErrors is { Count: > 0 } fields)
            {
                builder.SetExtension("fields", fields.ToDictionary(f => f.Key, f => (object?)f.Value));
            }
            return new GraphQLException(builder.Build());
        }
    }
}
