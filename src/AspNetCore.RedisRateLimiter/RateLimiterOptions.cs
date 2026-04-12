using Microsoft.AspNetCore.Http;

namespace AspNetCore.RedisRateLimiter;

/// <summary>
/// Configuration options for the Redis rate limiting middleware.
/// </summary>
/// <remarks>
/// Author: B.Yahia
/// </remarks>
public sealed class RateLimiterOptions
{
    /// <summary>
    /// Gets or sets the maximum number of requests permitted within the <see cref="Window"/>.
    /// Defaults to <c>100</c>.
    /// </summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>
    /// Gets or sets the time window for rate limiting.
    /// Defaults to <c>1 minute</c>.
    /// </summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the function used to extract a unique client identifier from the incoming HTTP request.
    /// Defaults to extracting the client's remote IP address.
    /// </summary>
    /// <example>
    /// <code>
    /// // Use an API key header as the client identifier:
    /// options.ClientIdExtractor = context =>
    ///     context.Request.Headers["X-API-Key"].FirstOrDefault() ?? "anonymous";
    /// </code>
    /// </example>
    public Func<HttpContext, string> ClientIdExtractor { get; set; } = context =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>
    /// Gets or sets the Redis key prefix used for rate limit counters.
    /// Defaults to <c>"rl:"</c>.
    /// </summary>
    public string RedisKeyPrefix { get; set; } = "rl:";

    /// <summary>
    /// Gets or sets the response message returned when a client exceeds the rate limit.
    /// Defaults to a standard JSON error payload.
    /// </summary>
    public string RateLimitExceededMessage { get; set; } =
        """{"error":"Rate limit exceeded. Please retry after the duration indicated in the Retry-After header."}""";
}
