using AspNetCore.RedisRateLimiter.Core;
using AspNetCore.RedisRateLimiter.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace AspNetCore.RedisRateLimiter.Tests;

/// <summary>
/// Unit tests for <see cref="RateLimitingMiddleware"/>.
/// Validates the middleware correctly allows or denies requests based on rate limit evaluation.
/// </summary>
public class RateLimitingMiddlewareTests
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly RateLimiterOptions _options;

    public RateLimitingMiddlewareTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockRedis
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);

        _options = new RateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            ClientIdExtractor = _ => "test-client"
        };
    }

    [Fact]
    public async Task InvokeAsync_WhenUnderLimit_ReturnsOkAndCallsNext()
    {
        // Arrange
        // Simulate Lua script returning: { allowed=1, remaining=9, retryAfter=0 }
        var luaResult = RedisResult.Create(
        [
            RedisResult.Create(1, ResultType.Integer),
            RedisResult.Create(9, ResultType.Integer),
            RedisResult.Create(0, ResultType.Integer)
        ]);

        _mockDatabase
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(luaResult);

        var context = CreateHttpContext();
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var options = Options.Create(_options);
        var rateLimiter = new RedisLuaRateLimiter(_mockRedis.Object, options);
        var middleware = new RateLimitingMiddleware(next, rateLimiter, options);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue("the request is under the limit and should proceed to the next middleware");
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("10");
        context.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("9");
    }

    [Fact]
    public async Task InvokeAsync_WhenLimitExceeded_Returns429WithRetryAfterHeader()
    {
        // Arrange
        // Simulate Lua script returning: { allowed=0, remaining=0, retryAfter=30000ms }
        var luaResult = RedisResult.Create(
        [
            RedisResult.Create(0, ResultType.Integer),
            RedisResult.Create(0, ResultType.Integer),
            RedisResult.Create(30000, ResultType.Integer) // 30 seconds in ms
        ]);

        _mockDatabase
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(luaResult);

        var context = CreateHttpContext();
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var options = Options.Create(_options);
        var rateLimiter = new RedisLuaRateLimiter(_mockRedis.Object, options);
        var middleware = new RateLimitingMiddleware(next, rateLimiter, options);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeFalse("the request exceeded the rate limit and should be short-circuited");
        context.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        context.Response.Headers["Retry-After"].ToString().Should().Be("30");
        context.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("0");
        context.Response.ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task InvokeAsync_WhenRedisReturnsNull_FailsOpenAndCallsNext()
    {
        // Arrange — Redis returns a null/empty result (fail-open scenario)
        _mockDatabase
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(RedisValue.Null));

        var context = CreateHttpContext();
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var options = Options.Create(_options);
        var rateLimiter = new RedisLuaRateLimiter(_mockRedis.Object, options);
        var middleware = new RateLimitingMiddleware(next, rateLimiter, options);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue("the middleware should fail open when Redis returns an unexpected result");
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    /// <summary>
    /// Creates a minimal <see cref="HttpContext"/> with an in-memory response body for testing.
    /// </summary>
    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }
}
