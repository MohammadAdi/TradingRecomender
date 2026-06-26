using System.Net;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace TradingRecommender.Infrastructure.Http;

/// <summary>
/// Centralised Polly policies used by the typed HttpClient registered with
/// <c>AddHttpClient&lt;IGoApiClient, GoApiClient&gt;()</c>.
///
/// <para>
/// Retry: 5 attempts with exponential backoff (2^n seconds).</para>
/// <para>
/// Circuit breaker: opens after 5 consecutive transient failures for 30s.</para>
/// </summary>
public static class ResiliencePolicies
{
    public const int RetryAttempts = 5;
    public const int CircuitBreakerThreshold = 5;
    public static readonly TimeSpan CircuitBreakerDuration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Wait-and-retry for transient HTTP failures (5xx, 408, network errors, 429).
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(ILogger logger)
        => HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: RetryAttempts,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, delay, attempt, _) =>
                {
                    // SECURITY: log only the exception type, not the full exception —
                    // some HttpRequestException implementations echo the request URI
                    // (and any header info) in their Message property.
                    var exType = outcome.Exception?.GetType().Name ?? "none";
                    var status = outcome.Result?.StatusCode.ToString() ?? "n/a";
                    logger.LogWarning(
                        "GOAPI retry {Attempt}/{Max} after {DelaySeconds}s (status={Status}, ex={ExType})",
                        attempt, RetryAttempts, delay.TotalSeconds, status, exType);
                });

    /// <summary>
    /// Circuit breaker: stop calling GOAPI for 30s after 5 consecutive failures.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(ILogger logger)
        => HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: CircuitBreakerThreshold,
                durationOfBreak: CircuitBreakerDuration,
                onBreak: (outcome, breakDelay) =>
                    logger.LogError(
                        "GOAPI circuit OPENED for {BreakSeconds}s (ex={ExType})",
                        breakDelay.TotalSeconds, outcome.Exception?.GetType().Name ?? "none"),
                onReset: () => logger.LogInformation("GOAPI circuit RESET"));
}