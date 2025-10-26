namespace ZitadelSDK.Services;

/// <summary>
/// Options for configuring the ZITADEL SDK client.
/// Authentication is configured separately via WithJwtAuth() or WithPatAuth() methods.
/// </summary>
public class ZitadelClientOptions
{
    /// <summary>
    /// The configuration section name for ZITADEL client options.
    /// </summary>
    public const string SectionName = "ServiceAdmin";

    /// <summary>
    /// Gets or sets the ZITADEL authority URL (e.g., https://your-instance.zitadel.cloud).
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the authentication type (default: "Bearer").
    /// </summary>
    public string AuthenticationType { get; set; } = "Bearer";
}