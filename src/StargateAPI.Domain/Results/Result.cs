using System.Diagnostics.CodeAnalysis;

namespace StargateAPI.Domain.Results
{
    /// <summary>The outcome of an application operation: a value, or an expected <see cref="AppError"/>.</summary>
    public sealed class Result<T>
    {
        private Result(T? value, AppError? error)
        {
            Value = value;
            Error = error;
        }

        public T? Value { get; }

        public AppError? Error { get; }

        [MemberNotNullWhen(true, nameof(Value))]
        [MemberNotNullWhen(false, nameof(Error))]
        public bool IsSuccess => Error is null;

        public static Result<T> Success(T value) => new(value, null);

        public static Result<T> Failure(AppError error) => new(default, error);

        public static implicit operator Result<T>(T value) => Success(value);

        public static implicit operator Result<T>(AppError error) => Failure(error);
    }
}
