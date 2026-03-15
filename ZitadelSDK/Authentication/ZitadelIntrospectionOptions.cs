using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace ZitadelSDK.Authentication;

/// <summary>
/// Options for configuring ZITADEL OAuth2 introspection authentication.
/// </summary>
public class ZitadelIntrospectionOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// The JWT Bearer client assertion type constant.
    /// </summary>
    public const string JwtBearerClientAssertionType = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";

    /// <summary>
    /// Gets or sets the ZITADEL authority URL (e.g., https://your-instance.zitadel.cloud).
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// Gets or sets the OAuth2 introspection endpoint.
    /// Defaults to {Authority}/oauth/v2/introspect when not set.
    /// </summary>
    public string? IntrospectionEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the authentication type for created identities.
    /// </summary>
    public string? AuthenticationType { get; set; }

    /// <summary>
    /// Gets or sets the client ID for introspection.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client secret for introspection.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets whether to enable caching of introspection results.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache duration for introspection results.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the cache key prefix used for introspection result entries.
    /// </summary>
    public string CacheKeyPrefix { get; set; } = "zitadel:introspection:";

    /// <summary>
    /// Gets or sets a custom cache key generator. Input is the token.
    /// </summary>
    public Func<string, string>? CacheKeyGenerator { get; set; }

    /// <summary>
    /// Gets or sets whether to skip introspection for tokens that look like JWTs.
    /// </summary>
    public bool SkipTokensWithDots { get; set; }

    /// <summary>
    /// Gets or sets whether to save the raw token in authentication properties.
    /// </summary>
    public bool SaveToken { get; set; }

    /// <summary>
    /// Gets or sets token type hint sent to the introspection endpoint.
    /// </summary>
    public string TokenTypeHint { get; set; } = "access_token";

    /// <summary>
    /// Gets or sets how many times to retry introspection when the token is reported as inactive.
    /// Useful when a newly issued token is not immediately visible to introspection due to propagation delay.
    /// </summary>
    public int InactiveTokenRetryCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets the delay between retries when introspection returns an inactive token.
    /// </summary>
    public TimeSpan InactiveTokenRetryDelay { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// Gets or sets the claim type used as name claim.
    /// </summary>
    public string NameClaimType { get; set; } = ClaimTypes.NameIdentifier;

    /// <summary>
    /// Gets or sets the claim type used as role claim.
    /// </summary>
    public string RoleClaimType { get; set; } = ClaimTypes.Role;

    /// <summary>
    /// Gets or sets custom token retriever logic.
    /// </summary>
    public Func<HttpRequest, string?>? TokenRetriever { get; set; }

    /// <summary>
    /// Gets or sets credential style used for client credentials.
    /// </summary>
    public ClientCredentialStyle ClientCredentialStyle { get; set; } = ClientCredentialStyle.AuthorizationHeader;

    /// <summary>
    /// Gets or sets authorization header style used for basic authentication.
    /// </summary>
    public BasicAuthenticationHeaderStyle AuthorizationHeaderStyle { get; set; } = BasicAuthenticationHeaderStyle.Rfc6749;

    /// <summary>
    /// Gets or sets introspection event handlers.
    /// </summary>
    public new ZitadelIntrospectionEvents Events { get; set; } = new();

    /// <summary>
    /// Gets or sets the JWT profile for client assertion authentication.
    /// When set, this will be used instead of ClientId/ClientSecret.
    /// </summary>
    public JwtProfileConfig? JwtProfile { get; set; }

    /// <inheritdoc />
    public override void Validate()
    {
        base.Validate();

        if (string.IsNullOrWhiteSpace(Authority))
        {
            throw new InvalidOperationException("ZITADEL Authority must be configured for introspection.");
        }

        if (JwtProfile is null && string.IsNullOrWhiteSpace(ClientId))
        {
            throw new InvalidOperationException(
                "Either a JwtProfile or ClientId must be configured for introspection authentication.");
        }

        if (InactiveTokenRetryCount < 0)
        {
            throw new InvalidOperationException("InactiveTokenRetryCount cannot be negative.");
        }

        if (InactiveTokenRetryDelay < TimeSpan.Zero)
        {
            throw new InvalidOperationException("InactiveTokenRetryDelay cannot be negative.");
        }
    }
}

/// <summary>
/// Specifies how client credentials are sent to the introspection endpoint.
/// </summary>
public enum ClientCredentialStyle
{
    /// <summary>
    /// Sends client credentials in the Authorization header.
    /// </summary>
    AuthorizationHeader,

    /// <summary>
    /// Sends client credentials in the request body.
    /// </summary>
    PostBody
}

/// <summary>
/// Specifies how Basic auth credentials are formatted before base64 encoding.
/// </summary>
public enum BasicAuthenticationHeaderStyle
{
    /// <summary>
    /// Uses RFC 6749 escaping for client id and client secret.
    /// </summary>
    Rfc6749,

    /// <summary>
    /// Uses classic RFC 2617 formatting without escaping.
    /// </summary>
    Rfc2617
}