namespace PhotoWorld.Web.Services;

/// <summary>Outcome of an API call that can fail validation. Errors are keyed by API field name.</summary>
public sealed record ApiResult<T>(T? Value, IReadOnlyDictionary<string, string[]> Errors)
{
    public bool Succeeded => Value is not null;

    public static ApiResult<T> Success(T value) => new(value, new Dictionary<string, string[]>());

    public static ApiResult<T> Failure(IReadOnlyDictionary<string, string[]> errors) => new(default, errors);

    public static ApiResult<T> Failure(string message) => Failure(new Dictionary<string, string[]> { [""] = [message] });
}

/// <summary>The API rejected the user's token (expired or user gone). Handled globally by signing out.</summary>
public sealed class ApiUnauthorizedException() : Exception("The PhotoWorld API session has expired.");
