using Microsoft.AspNetCore.Builder;

namespace AspNetCore.RedisRateLimiter.Extensions;

/// <summary>
/// Extension methods for adding the Redis rate limiting middleware to the ASP.NET Core request pipeline.
/// </summary>
/// <remarks>
/// Author: B.Yahia
/// </remarks>
public static class RateLimiterApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Redis rate limiting middleware to the application's request pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// var app = builder.Build();
    /// app.UseRedisRateLimiting();
    /// app.MapControllers();
    /// app.Run();
    /// </code>
    /// </example>
    public static IApplicationBuilder UseRedisRateLimiting(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<RateLimitingMiddleware>();
    }
}
