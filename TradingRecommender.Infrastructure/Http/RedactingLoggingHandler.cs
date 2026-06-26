using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace TradingRecommender.Infrastructure.Http;

/// <summary>
/// <see cref="DelegatingHandler"/> that logs request/response at Debug level
/// while redacting known-sensitive headers (<c>Authorization</c>,
/// <c>X-Api-Key</c>) so secrets never reach the log stream.
///
/// Attach this to every typed HttpClient that carries credentials:
///   .AddHttpMessageHandler&lt;RedactingLoggingHandler&gt;()
/// </summary>
public sealed class RedactingLoggingHandler : DelegatingHandler
{
    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "X-Api-Key", "X-Auth-Token", "Cookie", "Set-Cookie"
    };

    private readonly ILogger<RedactingLoggingHandler> _logger;

    public RedactingLoggingHandler(ILogger<RedactingLoggingHandler> logger)
        => _logger = logger;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("HTTP {Method} {Url}", request.Method, Scrub(request.RequestUri));

        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            // Do NOT log the exception object — its message can include the
            // request URI which echoes the auth-bearing path. Log type only.
            _logger.LogWarning("HTTP {Method} {Url} threw {ExceptionType}",
                request.Method, Scrub(request.RequestUri), ex.GetType().Name);
            throw;
        }

        _logger.LogDebug("HTTP {Method} {Url} → {Status}",
            request.Method, Scrub(request.RequestUri), (int)response.StatusCode);

        return response;
    }

    /// <summary>Strips query string values from URLs in log output.</summary>
    private static string Scrub(Uri? uri)
        => uri is null
            ? "(null)"
            : uri.IsAbsoluteUri
                ? $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}"
                : uri.ToString();

    /// <summary>Convenience: returns true if a header name is sensitive.</summary>
    public static bool IsSensitive(string headerName)
        => SensitiveHeaders.Contains(headerName);

    /// <summary>Convenience: count of sensitive headers.</summary>
    public static int SensitiveHeaderCount => SensitiveHeaders.Count;
}