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
    public async Task<string> GetSignedJwtAsync(string authority)
    {
        await Task.CompletedTask; // Placeholder for async operations

        var rsaKey = RSA.Create();
        rsaKey.ImportFromPem(Key);

        var securityKey = new RsaSecurityKey(rsaKey)
        {
            KeyId = KeyId
        };

        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, ClientId),
            new(JwtRegisteredClaimNames.Iss, ClientId),
            new(JwtRegisteredClaimNames.Aud, authority)
        };

        if (!string.IsNullOrWhiteSpace(UserId))
        {
            claims.Add(new Claim("urn:zitadel:iam:user:id", UserId));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(1),
            IssuedAt = DateTime.UtcNow,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
