using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ZitadelSDK.Services;

namespace ZitadelSDK.Extensions;

/// <summary>
/// Health check for ZITADEL service availability.
/// Checks the /debug/ready endpoint to determine if ZITADEL is healthy.
/// </summary>
public class ZitadelHealthCheck : IHealthCheck
{
    private readonly string _authority;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZitadelHealthCheck"/> class.
    /// </summary>
    /// <param name="authority">The ZITADEL authority URL (e.g., https://your-instance.zitadel.cloud).</param>
    /// <param name="httpClientFactory">Factory for creating HTTP clients.</param>
    public ZitadelHealthCheck(
        string authority,
        IHttpClientFactory httpClientFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authority);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        _authority = authority;
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10); // 10 second timeout for health checks

            var readyUrl = BuildReadyUrl(_authority);

            using var response = await client.GetAsync(readyUrl, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("ZITADEL service is healthy");
            }
            else
            {
                return HealthCheckResult.Unhealthy(
                    $"ZITADEL service returned unhealthy status: {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            return HealthCheckResult.Unhealthy(
                "Unable to connect to ZITADEL service",
                exception: ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy(
                "ZITADEL health check timed out",
                exception: ex);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Unexpected error during ZITADEL health check",
                exception: ex);
        }
    }

    private static string BuildReadyUrl(string authority)
    {
        if (string.IsNullOrWhiteSpace(authority))
        {
            throw new InvalidOperationException("ZITADEL authority is not configured");
        }

        if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri))
        {
            throw new InvalidOperationException($"Invalid ZITADEL authority: {authority}");
        }

        var readyUri = new Uri(authorityUri, "/debug/ready");
        return readyUri.ToString();
    }
}