using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ZitadelSDK.Authentication;
using ZitadelSDK.Internal;

namespace ZitadelSDK.Credentials;

/// <summary>
/// Represents ZITADEL application credentials for JWT profile authentication.
/// </summary>
public class Application
{
    /// <summary>
    /// Gets or sets the application ID.
    /// </summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client ID.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the RSA private key in PEM format (PKCS#8 "PRIVATE KEY" or PKCS#1 "RSA PRIVATE KEY").
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key ID.
    /// </summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user ID (optional, for user impersonation).
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Generates a signed JWT assertion for client authentication.
    /// </summary>
    /// <param name="authority">The ZITADEL authority URL.</param>
    /// <returns>A signed JWT token.</returns>
    public Task<string> GetSignedJwtAsync(string authority)
    {
        return GetSignedJwtAsync(authority, allowInsecureTransport: false);
    }

    /// <summary>
    /// Generates a signed JWT assertion for client authentication.
    /// </summary>
    /// <param name="authority">The ZITADEL authority URL.</param>
    /// <param name="allowInsecureTransport">Whether plaintext HTTP transport is allowed for the assertion audience.</param>
    /// <returns>A signed JWT token.</returns>
    public Task<string> GetSignedJwtAsync(string authority, bool allowInsecureTransport = false)
    {
        if (string.IsNullOrWhiteSpace(authority))
        {
            throw new ArgumentException("Authority must be provided to generate a JWT assertion.", nameof(authority));
        }

        var normalizedAuthority = TransportSecurity.NormalizeAuthority(
            authority,
            "Authority",
            allowInsecureTransport);

        var credentials = JwtSigningCredentialsFactory.CreateFromPem(Key, KeyId);

        var now = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, ClientId),
            new(JwtRegisteredClaimNames.Iss, ClientId),
            new(JwtRegisteredClaimNames.Aud, normalizedAuthority)
        };

        if (!string.IsNullOrWhiteSpace(UserId))
        {
            claims.Add(new Claim("urn:zitadel:iam:user:id", UserId));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = now.AddMinutes(5), // Short-lived assertion
            IssuedAt = now,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return Task.FromResult(tokenHandler.WriteToken(token));
    }
}
