using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace ZitadelSDK.Authentication;

/// <summary>
/// JWT Profile configuration for service account authentication.
/// </summary>
public class JwtProfileConfig
{
    /// <summary>
    /// Gets or sets the key ID.
    /// </summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the RSA private key in PEM format.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user ID (service account user ID).
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the application/client ID.
    /// </summary>
    public string? AppId { get; set; }

    /// <summary>
    /// Gets or sets the client ID (alternative to AppId).
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Validates that all required fields are configured.
    /// </summary>
    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(KeyId) &&
        !string.IsNullOrWhiteSpace(Key) &&
        (!string.IsNullOrWhiteSpace(UserId) || !string.IsNullOrWhiteSpace(AppId) || !string.IsNullOrWhiteSpace(ClientId));

    /// <summary>
    /// Gets the effective user ID for the JWT assertion.
    /// Falls back to AppId or ClientId if UserId is not explicitly set.
    /// For introspection, ClientId should be used for iss/sub claims.
    /// </summary>
    private string GetEffectiveUserId()
    {
        // For introspection, prefer ClientId first (as per ZITADEL Application pattern)
        if (!string.IsNullOrWhiteSpace(ClientId))
            return ClientId;
        if (!string.IsNullOrWhiteSpace(AppId))
            return AppId;
        if (!string.IsNullOrWhiteSpace(UserId))
            return UserId;
        throw new InvalidOperationException("UserId, AppId, or ClientId must be set.");
    }

    /// <summary>
    /// Gets the effective user ID for the JWT assertion (public helper).
    /// Falls back to AppId or ClientId if UserId is not explicitly set.
    /// </summary>
    internal string GetUserId() => GetEffectiveUserId();

    /// <summary>
    /// Generates a signed JWT assertion for authentication with ZITADEL.
    /// </summary>
    /// <param name="authority">The ZITADEL authority URL.</param>
    /// <returns>A signed JWT token string.</returns>
    public Task<string> GetSignedJwtAsync(string authority)
    {
        if (string.IsNullOrWhiteSpace(authority))
        {
            throw new ArgumentException("Authority must be provided to generate a JWT assertion.", nameof(authority));
        }

        if (!Uri.TryCreate(authority, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException($"Invalid authority '{authority}'. Provide an absolute HTTPS URL.");
        }

        if (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("JWT Profile assertions require an HTTPS authority.");
        }

        var normalizedAuthority = baseUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');

        var rsaKey = RSA.Create();
        rsaKey.ImportFromPem(Key);

        var securityKey = new RsaSecurityKey(rsaKey)
        {
            KeyId = KeyId
        };

        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

        var now = DateTime.UtcNow;
        var effectiveUserId = GetEffectiveUserId();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, effectiveUserId),
            new(JwtRegisteredClaimNames.Iss, effectiveUserId),
            new(JwtRegisteredClaimNames.Aud, normalizedAuthority),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Exp, new DateTimeOffset(now.AddMinutes(5)).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = now.AddMinutes(5), // Short-lived assertion
            IssuedAt = now,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var jwt = tokenHandler.WriteToken(token);

        return Task.FromResult(jwt);
    }
}
