using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AspNetCore.RedisRateLimiter.Core;

/// <summary>
/// Performs atomic rate limit evaluation against Redis using a Lua script.
/// Implements a sliding window algorithm to ensure accurate, race-condition-free request counting.
/// Utilizes the internal Redis clock to eliminate distributed clock drift across horizontally scaled API nodes.
/// </summary>
/// <remarks>
/// Author: B.Yahia
/// </remarks>
internal sealed class RedisLuaRateLimiter
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RateLimiterOptions _options;

    /// <summary>
    /// The Lua script implementing a sliding window rate limiter.
    /// 
    /// KEYS[1] = the rate limit key for this client
    /// ARGV[1] = window size in milliseconds
    /// ARGV[2] = permit limit (max requests allowed in window)
    /// ARGV[3] = unique request identifier (random string/number, for sorted set member)
    ///
    /// Note: The current timestamp is generated internally by Redis using the TIME command 
    /// to ensure consistency across distributed nodes.
    ///
    /// Returns: { allowed (0|1), remaining, retryAfterMs }
    /// </summary>
    private const string SlidingWindowLuaScript = """
        local key = KEYS[1]
        local window = tonumber(ARGV[1]) 
        local limit = tonumber(ARGV[2])  
        local member = ARGV[3]           
        
        -- Get time directly from Redis server to prevent local API clock drift
        local redis_time = redis.call('TIME')
        local now = tonumber(redis_time[1]) * 1000 + math.floor(tonumber(redis_time[2]) / 1000)
        
        -- Remove all entries outside the current sliding window
        redis.call('ZREMRANGEBYSCORE', key, 0, now - window)
        
        -- Count current requests in window
        local current = redis.call('ZCARD', key)
        
        if current < limit then
            -- Add the new request with its timestamp as the score
            redis.call('ZADD', key, now, member)
            -- Set key expiry to auto-cleanup
            redis.call('PEXPIRE', key, window)
            
            return {1, limit - current - 1, 0}
        else
            -- Get the oldest entry to calculate retry-after
            local oldest = redis.call('ZRANGE', key, 0, 0, 'WITHSCORES')
            local retryAfter = 0
            if #oldest > 0 then
                retryAfter = tonumber(oldest[2]) + window - now
                if retryAfter < 0 then retryAfter = 0 end
            end
            
            return {0, 0, retryAfter}
        end
        """;

    public RedisLuaRateLimiter(IConnectionMultiplexer redis, IOptions<RateLimiterOptions> options)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Evaluates whether the specified client is within their rate limit.
    /// </summary>
    /// <param name="clientId">The unique client identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="RateLimitResult"/> containing the evaluation outcome.</returns>
    public async Task<RateLimitResult> EvaluateAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = $"{_options.RedisKeyPrefix}{clientId}";
        var windowMs = (long)_options.Window.TotalMilliseconds;
        
        // Fast, low-allocation unique identifier for the sorted set
        var uniqueMember = Random.Shared.NextInt64().ToString();

        var keys = new RedisKey[] { key };
        
        var values = new RedisValue[]
        {
            windowMs,             // Maps to ARGV[1]
            _options.PermitLimit, // Maps to ARGV[2]
            uniqueMember          // Maps to ARGV[3]
        };

        RedisResult[]? result;

        try
        {
            var rawResult = await db.ScriptEvaluateAsync(
                SlidingWindowLuaScript,
                keys,
                values);

            result = (RedisResult[]?)rawResult;
        }
        catch (RedisException)
        {
            // Fail open: Keep the API online if the Redis node crashes or network partitions
            return RateLimitResult.Allowed(_options.PermitLimit);
        }
        catch (InvalidCastException)
        {
            // Fail open: allow the request if Redis returns an unexpected (non-array) result
            return RateLimitResult.Allowed(_options.PermitLimit);
        }

        if (result is null || result.Length < 3)
        {
            return RateLimitResult.Allowed(_options.PermitLimit);
        }

        var allowed = (int)result[0] == 1;
        var remaining = (int)result[1];
        var retryAfterMs = (long)result[2];

        return allowed
            ? RateLimitResult.Allowed(remaining)
            : RateLimitResult.Denied(
                remaining: 0,
                retryAfter: TimeSpan.FromMilliseconds(retryAfterMs));
    }
}

/// <summary>
/// Represents the result of a rate limit evaluation.
/// </summary>
internal sealed class RateLimitResult
{
    /// <summary>
    /// Gets a value indicating whether the request is allowed.
    /// </summary>
    public bool IsAllowed { get; private init; }

    /// <summary>
    /// Gets the number of remaining requests in the current window.
    /// </summary>
    public int Remaining { get; private init; }

    /// <summary>
    /// Gets the duration the client should wait before retrying.
    /// Only meaningful when <see cref="IsAllowed"/> is <c>false</c>.
    /// </summary>
    public TimeSpan RetryAfter { get; private init; }

    public static RateLimitResult Allowed(int remaining) => new()
    {
        IsAllowed = true,
        Remaining = remaining,
        RetryAfter = TimeSpan.Zero
    };

    public static RateLimitResult Denied(int remaining, TimeSpan retryAfter) => new()
    {
        IsAllowed = false,
        Remaining = remaining,
        RetryAfter = retryAfter
    };
}