namespace KeyEnv;

/// <summary>
/// Exception thrown by the KeyEnv SDK for API and configuration errors.
/// </summary>
public class KeyEnvException : Exception
{
    /// <summary>
    /// HTTP status code from the API response (0 for non-HTTP errors).
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Optional error code for programmatic handling.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Creates a new KeyEnvException.
    /// </summary>
    public KeyEnvException(string message, int statusCode = 0, string? errorCode = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Creates a new KeyEnvException with an inner exception.
    /// </summary>
    public KeyEnvException(string message, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = 0;
        ErrorCode = null;
    }

    /// <summary>
    /// Returns true if this is a 404 Not Found error.
    /// </summary>
    public bool IsNotFound => StatusCode == 404;

    /// <summary>
    /// Returns true if this is a 401 Unauthorized error.
    /// </summary>
    public bool IsUnauthorized => StatusCode == 401;

    /// <summary>
    /// Returns true if this is a 403 Forbidden error.
    /// </summary>
    public bool IsForbidden => StatusCode == 403;

    /// <summary>
    /// Returns true if this is a 409 Conflict error.
    /// </summary>
    public bool IsConflict => StatusCode == 409;

    /// <summary>
    /// Returns true if this is a 429 Rate Limited error.
    /// </summary>
    public bool IsRateLimited => StatusCode == 429;

    /// <summary>
    /// Returns true if this is a 5xx server error.
    /// </summary>
    public bool IsServerError => StatusCode >= 500 && StatusCode < 600;

    /// <inheritdoc/>
    public override string ToString()
    {
        var result = $"KeyEnvException: {Message}";
        if (StatusCode > 0)
        {
            result += $" (status={StatusCode}";
            if (!string.IsNullOrEmpty(ErrorCode))
            {
                result += $", code={ErrorCode}";
            }
            result += ")";
        }
        return result;
    }

    /// <summary>
    /// Creates an API error exception.
    /// </summary>
    internal static KeyEnvException Api(int statusCode, string message, string? code = null)
        => new(message, statusCode, code);

    /// <summary>
    /// Creates a configuration error exception.
    /// </summary>
    internal static KeyEnvException Config(string message)
        => new($"Configuration error: {message}");

    /// <summary>
    /// Creates a timeout error exception.
    /// </summary>
    internal static KeyEnvException Timeout()
        => new("Request timeout", 408);
}
