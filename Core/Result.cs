namespace Narratum.Core;

/// <summary>
/// Encapsulates a result with either a value or an error.
/// </summary>
public abstract record Result<T>
{
    public sealed record Success(T Value) : Result<T>;
    public sealed record Failure(string Message, Exception? Exception = null) : Result<T>;

    public static Result<T> Ok(T value) => new Success(value);
    public static Result<T> Fail(string message, Exception? ex = null) => new Failure(message, ex);

    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<string, TResult> onFailure) =>
        this switch
        {
            Success s => onSuccess(s.Value),
            Failure f => onFailure(f.Message),
            _ => throw new InvalidOperationException("Unknown result type")
        };

    public async Task<TResult> MatchAsync<TResult>(
        Func<T, Task<TResult>> onSuccess,
        Func<string, Task<TResult>> onFailure) =>
        this switch
        {
            Success s => await onSuccess(s.Value),
            Failure f => await onFailure(f.Message),
            _ => throw new InvalidOperationException("Unknown result type")
        };
}
