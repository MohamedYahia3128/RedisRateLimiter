using AspNetCore.RedisRateLimiter.Core;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AspNetCore.RedisRateLimiter.Extensions;

/// <summary>
/// Extension methods for registering Redis rate limiting services in the DI container.
/// </summary>
/// <remarks>
/// Author: B.Yahia
/// </remarks>
public static class RateLimiterServiceCollectionExtensions
{
    /// <summary>
    /// Adds Redis-backed rate limiting services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="redisConnectionString">The Redis connection string (e.g., <c>"localhost:6379"</c>).</param>
    /// <param name="configureOptions">An optional action to configure <see cref="RateLimiterOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddRedisRateLimiting("localhost:6379", options =>
    /// {
    ///     options.PermitLimit = 60;
    ///     options.Window = TimeSpan.FromMinutes(1);
    ///     options.ClientIdExtractor = ctx => ctx.Request.Headers["X-API-Key"].FirstOrDefault() ?? "anonymous";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddRedisRateLimiting(
        this IServiceCollection services,
        string redisConnectionString,
        Action<RateLimiterOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(redisConnectionString);

        // Register the Redis connection multiplexer as a singleton
        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(redisConnectionString));

        // Configure the rate limiter options
        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<RateLimiterOptions>(_ => { });
        }

        // Register the internal rate limiter
        services.AddSingleton<RedisLuaRateLimiter>();

        return services;
    }

    /// <summary>
    /// Adds Redis-backed rate limiting services using an existing <see cref="IConnectionMultiplexer"/> instance.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="connectionMultiplexer">An existing Redis connection multiplexer.</param>
    /// <param name="configureOptions">An optional action to configure <see cref="RateLimiterOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddRedisRateLimiting(
        this IServiceCollection services,
        IConnectionMultiplexer connectionMultiplexer,
        Action<RateLimiterOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);

        services.AddSingleton(connectionMultiplexer);

        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<RateLimiterOptions>(_ => { });
        }

        services.AddSingleton<RedisLuaRateLimiter>();

        return services;
    }
}
