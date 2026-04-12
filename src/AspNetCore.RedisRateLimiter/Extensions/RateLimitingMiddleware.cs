using AspNetCore.RedisRateLimiter.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AspNetCore.RedisRateLimiter.Extensions;

/// <summary>
/// ASP.NET Core middleware that enforces Redis-backed rate limiting on incoming HTTP requests.
/// </summary>
/// <remarks>
/// Author: B.Yahia
/// </remarks>
internal sealed class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RedisLuaRateLimiter _rateLimiter;
    private readonly RateLimiterOptions _options;

    public RateLimitingMiddleware(
        RequestDelegate next,
        RedisLuaRateLimiter rateLimiter,
        IOptions<RateLimiterOptions> options)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientId = _options.ClientIdExtractor(context);

        var result = await _rateLimiter.EvaluateAsync(clientId, context.RequestAborted);

        // Always attach rate limit headers for observability
        context.Response.Headers["X-RateLimit-Limit"] = _options.PermitLimit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = result.Remaining.ToString();

        if (!result.IsAllowed)
        {
            var retryAfterSeconds = (int)Math.Ceiling(result.RetryAfter.TotalSeconds);

            context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
            context.Response.Headers["X-RateLimit-Reset"] = retryAfterSeconds.ToString();
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(_options.RateLimitExceededMessage);
            return;
        }

        await _next(context);
    }
}
