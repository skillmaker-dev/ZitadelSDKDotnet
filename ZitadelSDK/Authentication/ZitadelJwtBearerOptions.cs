namespace ZitadelSDK.Authentication;

/// <summary>
/// Options for configuring ZITADEL JWT Bearer authentication.
/// </summary>
public class ZitadelJwtBearerOptions
{
    /// <summary>
    /// Gets or sets the ZITADEL authority URL (e.g., https://your-instance.zitadel.cloud).
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expected audience(s) for JWT tokens.
    /// ZITADEL tokens may contain multiple audiences in an array.
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Gets or sets the expected audiences for JWT tokens.
    /// Use this when you need to validate against multiple possible audiences.
    /// ZITADEL tokens often contain multiple audiences in an array.
    /// </summary>
    public IEnumerable<string>? Audiences { get; set; }

    /// <summary>
    /// Gets or sets whether to require HTTPS metadata.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to save the token in the authentication properties.
    /// </summary>
    public bool SaveToken { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate the audience.
    /// </summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate the issuer.
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate the token lifetime.
    /// </summary>
    public bool ValidateLifetime { get; set; } = true;

    /// <summary>
    /// Gets or sets the clock skew to apply when validating token lifetime.
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the name claim type.
    /// </summary>
    public string NameClaimType { get; set; } = "name";

    /// <summary>
    /// Gets or sets the role claim type.
    /// </summary>
    public string RoleClaimType { get; set; } = ZitadelClaimTypes.ProjectRoles;
}
