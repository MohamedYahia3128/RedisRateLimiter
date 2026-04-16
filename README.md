# AspNetCore.RedisRateLimiter

A production-grade, Redis-backed API rate limiting middleware for ASP.NET Core.

## Features

- **Atomic operations** — Uses Redis Lua scripts to prevent race conditions
- **Sliding window** algorithm for accurate request counting
- **Configurable** — Set limits, time windows, and client ID extraction strategies
- **Standard headers** — Returns `X-RateLimit-Limit`, `X-RateLimit-Remaining`, and `Retry-After`
- **Fail-open** — Gracefully allows requests if Redis is unavailable

## Quick Start

```csharp
// Program.cs
builder.Services.AddRedisRateLimiting("localhost:6379", options =>
{
    options.PermitLimit = 100;
    options.Window = TimeSpan.FromMinutes(1);
    options.ClientIdExtractor = ctx =>
        ctx.Request.Headers["X-API-Key"].FirstOrDefault() ?? "anonymous";
});

var app = builder.Build();
app.UseRedisRateLimiting();
```

## Headers

The middleware automatically attaches the following headers to every response for observability:

- `X-RateLimit-Limit`: The maximum number of requests allowed in the window.
- `X-RateLimit-Remaining`: The number of requests remaining in the current window.
- `Retry-After`: (Only on 429) The number of seconds to wait before retrying.
- `X-RateLimit-Reset`: (Only on 429) Same as `Retry-After`, for compatibility with other standards.

## Advanced Configuration

You can customize the rate limiting behavior via `RateLimiterOptions`:

```csharp
builder.Services.AddRedisRateLimiting("localhost:6379", options =>
{
    // The maximum requests allowed in the window
    options.PermitLimit = 50;

    // The sliding window duration
    options.Window = TimeSpan.FromSeconds(30);

    // Prefix for Redis keys to avoid collisions
    options.RedisKeyPrefix = "myapp:rl:";

    // Custom client identifier extraction (e.g., from a header or claim)
    options.ClientIdExtractor = context => 
        context.User.Identity?.Name ?? "anonymous";

    // Custom rejection message
    options.RateLimitExceededMessage = "{\"msg\": \"Slow down!\"}";
});
```

## Resilience

The library is designed with a **fail-open** strategy. If the Redis server is unreachable or a network exception occurs, the middleware will catch the error and allow the request to proceed to ensure your API remains available.

## License

MIT
