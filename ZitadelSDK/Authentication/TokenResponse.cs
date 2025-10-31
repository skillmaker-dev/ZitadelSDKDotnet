using System.Text.Json.Serialization;

namespace ZitadelSDK.Authentication;

/// <summary>
/// Response from ZITADEL OAuth token endpoint.
/// </summary>
public class TokenResponse
{
    /// <summary>
    /// Gets or sets the access token.
    /// </summary>
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the token type.
    /// </summary>
    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    /// <summary>
    /// Gets or sets the number of seconds until the token expires.
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Gets or sets the refresh token.
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the scope of the token.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}
