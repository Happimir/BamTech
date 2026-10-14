namespace StargateAPI.Domain.Results
{
    /// <summary>How an expected application failure should be classified. HTTP mapping happens in the controllers.</summary>
    public enum ErrorKind
    {
        Validation,
        NotFound,
        Conflict,
        Unavailable
    }

    /// <summary>Stable, client-facing error codes. Treat changes as API contract changes.</summary>
    public static class ErrorCodes
    {
        public const string Validation = "validation";
        public const string NotFound = "not_found";
        public const string AmbiguousName = "ambiguous_name";
        public const string ChronologyConflict = "chronology_conflict";
        public const string ConcurrencyConflict = "concurrency_conflict";
        public const string VersionRequired = "version_required";
        public const string FirstDutyRetirement = "first_duty_retirement";
        public const string AlreadyRetired = "already_retired";
        public const string ServiceUnavailable = "service_unavailable";
        public const string BadRequest = "bad_request";
        public const string InternalError = "internal_error";
    }

    /// <summary>An expected failure with a stable code and a message that is safe to show to clients.</summary>
    public sealed record AppError(
        ErrorKind Kind,
        string Code,
        string Message,
        IReadOnlyDictionary<string, string[]>? FieldErrors = null)
    {
        public static AppError NotFound(string message) =>
            new(ErrorKind.NotFound, ErrorCodes.NotFound, message);

        public static AppError AmbiguousName() =>
            new(ErrorKind.Conflict, ErrorCodes.AmbiguousName,
                "More than one person has this name. Select the person by ID instead.");

        /// <summary>The person changed after the client read it (see <c>Person.IsStale</c>).</summary>
        public static AppError StaleVersion() =>
            new(ErrorKind.Conflict, ErrorCodes.ConcurrencyConflict,
                "This person was changed by another request. Reload and review before trying again.");

        public static AppError Validation(string field, string message) =>
            new(ErrorKind.Validation, ErrorCodes.Validation, "One or more fields are invalid.",
                new Dictionary<string, string[]> { [field] = [message] });

        public static AppError Validation(IReadOnlyDictionary<string, string[]> fieldErrors) =>
            new(ErrorKind.Validation, ErrorCodes.Validation, "One or more fields are invalid.", fieldErrors);

        public static AppError Conflict(string code, string message) =>
            new(ErrorKind.Conflict, code, message);
    }
}
