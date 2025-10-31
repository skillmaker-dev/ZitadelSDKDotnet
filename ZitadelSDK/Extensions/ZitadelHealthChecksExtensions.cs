using Microsoft.Extensions.Diagnostics.HealthChecks;
using ZitadelSDK.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding ZITADEL health checks to ASP.NET Core.
/// </summary>
public static class ZitadelHealthChecksExtensions
{
    /// <summary>
    /// Adds a health check for ZITADEL service availability.
    /// The health check calls the /debug/ready endpoint to verify ZITADEL is operational.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="authority">The ZITADEL authority URL (e.g., https://your-instance.zitadel.cloud).</param>
    /// <param name="name">The name of the health check (default: "zitadel").</param>
    /// <param name="failureStatus">The health status to report when the health check fails (default: Unhealthy).</param>
    /// <param name="tags">Additional tags for the health check.</param>
    /// <returns>The health checks builder for method chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddHealthChecks()
    ///     .AddZitadel("https://my-zitadel-instance.com");
    /// </code>
    /// </example>
    public static IHealthChecksBuilder AddZitadel(
        this IHealthChecksBuilder builder,
        string authority,
        string name = "zitadel",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(authority);

        var healthCheck = new ZitadelHealthCheck(authority, builder.Services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>());

        return builder.AddCheck(
            name,
            healthCheck,
            failureStatus ?? HealthStatus.Unhealthy,
            tags ?? []);
    }
}