using Duende.AspNetCore.Authentication.OAuth2Introspection;

namespace ZitadelSDK.Authentication;

/// <summary>
/// Options for configuring ZITADEL OAuth2 introspection authentication.
/// </summary>
public class ZitadelIntrospectionOptions : OAuth2IntrospectionOptions
{
    /// <summary>
    /// The JWT Bearer client assertion type constant.
    /// </summary>
    public const string JwtBearerClientAssertionType = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";

    /// <summary>
    /// Gets or sets the ZITADEL authority URL (e.g., https://your-instance.zitadel.cloud).
    /// </summary>
    public new string? Authority { get; set; }

    /// <summary>
    /// Gets or sets the client ID for introspection.
    /// </summary>
    public new string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets whether to enable caching of introspection results.
    /// </summary>
    public new bool EnableCaching { get; set; }

    /// <summary>
    /// Gets or sets the cache duration for introspection results.
    /// </summary>
    public new TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the JWT profile for client assertion authentication.
    /// When set, this will be used instead of ClientId/ClientSecret.
    /// </summary>
    public JwtProfileConfig? JwtProfile { get; set; }
}