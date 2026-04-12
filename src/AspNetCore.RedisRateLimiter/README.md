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

## License

MIT
