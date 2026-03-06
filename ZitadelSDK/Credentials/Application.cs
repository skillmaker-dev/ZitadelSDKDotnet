using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

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
    /// Gets or sets the private key in JSON format.
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

        using var rsaKey = RSA.Create();
        rsaKey.ImportFromPem(Key);

        var securityKey = new RsaSecurityKey(rsaKey)
        {
            KeyId = KeyId
        };

        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

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
