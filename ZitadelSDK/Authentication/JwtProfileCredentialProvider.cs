using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;

namespace ZitadelSDK.Authentication;

/// <summary>
/// Provides JWT Profile authentication for ZITADEL service accounts.
/// </summary>
public class JwtProfileCredentialProvider : IZitadelCredentialProvider
{
    private readonly JwtProfileConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JwtProfileCredentialProvider> _logger;
    private readonly string _authenticationScheme;

    // Token cache
    private string? _cachedAccessToken;
    private DateTime _tokenExpiration = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtProfileCredentialProvider"/> class.
    /// </summary>
    /// <param name="config">The JWT profile configuration.</param>
    /// <param name="httpClientFactory">Factory for creating HTTP clients.</param>
    /// <param name="logger">Logger used for diagnostic messages.</param>
    /// <param name="authenticationScheme">The authentication scheme to apply to outbound requests.</param>
    public JwtProfileCredentialProvider(
        JwtProfileConfig config,
        IHttpClientFactory httpClientFactory,
        ILogger<JwtProfileCredentialProvider> logger,
        string authenticationScheme = "Bearer")
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(logger);

        if (!config.IsValid())
        {
            throw new ArgumentException("JWT Profile configuration is invalid. KeyId, Key, and UserId (or AppId/ClientId) are required.", nameof(config));
        }

        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _authenticationScheme = authenticationScheme;
    }

    /// <summary>
    /// Creates call credentials that obtain and cache JWT profile access tokens.
    /// </summary>
    /// <param name="authority">The ZITADEL authority URL.</param>
    /// <returns>Call credentials that attach the appropriate Authorization header.</returns>
    public CallCredentials CreateCallCredentials(string authority)
    {
        return CallCredentials.FromInterceptor(async (context, metadata) =>
        {
            var accessToken = await GetAccessTokenAsync(authority);
            metadata.Add("Authorization", $"{_authenticationScheme} {accessToken}");
        });
    }

    /// <summary>
    /// Gets an OAuth access token using JWT Profile authentication.
    /// Implements token caching and automatic refresh.
    /// </summary>
    private async Task<string> GetAccessTokenAsync(string authority)
    {
        // Check if we have a valid cached token (with 5 minute buffer before expiration)
        if (_cachedAccessToken != null && DateTime.UtcNow < _tokenExpiration.AddMinutes(-5))
        {
            _logger.LogDebug("Using cached access token, expires at {Expiration}", _tokenExpiration);
            return _cachedAccessToken;
        }

        // Use semaphore to prevent multiple simultaneous token requests
        await _tokenLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_cachedAccessToken != null && DateTime.UtcNow < _tokenExpiration.AddMinutes(-5))
            {
                return _cachedAccessToken;
            }

            _logger.LogInformation("Requesting new access token from ZITADEL");

            // Step 1: Generate JWT assertion
            var normalizedAuthority = NormalizeAuthority(authority);
            var tokenEndpoint = BuildTokenEndpoint(normalizedAuthority);

            var jwtAssertion = GenerateJwtAssertion(normalizedAuthority);

            // Step 2: Exchange JWT for OAuth access token
            var httpClient = _httpClientFactory.CreateClient();

            var requestData = new Dictionary<string, string>
            {
                { "grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer" },
                { "scope", "openid profile email urn:zitadel:iam:org:project:id:zitadel:aud" },
                { "assertion", jwtAssertion }
            };

            var response = await httpClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(requestData));

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to obtain access token. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, errorContent);
                throw new InvalidOperationException(
                    $"Failed to obtain access token from ZITADEL. Status: {response.StatusCode}, Response: {errorContent}");
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();

            if (tokenResponse?.AccessToken == null)
            {
                throw new InvalidOperationException("Token response did not contain an access token");
            }

            if (!string.Equals(tokenResponse.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Unsupported token type '{tokenResponse.TokenType}'. Expected 'Bearer'.");
            }

            // Cache the token
            _cachedAccessToken = tokenResponse.AccessToken;
            _tokenExpiration = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            _logger.LogInformation("Successfully obtained access token, expires in {ExpiresIn} seconds", tokenResponse.ExpiresIn);

            return _cachedAccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obtaining access token");
            throw;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>
    /// Generates a JWT assertion for JWT Profile authentication.
    /// This is the signed JWT that will be exchanged for an access token.
    /// </summary>
    private string GenerateJwtAssertion(string authority)
    {
        var rsaKey = RSA.Create();
        rsaKey.ImportFromPem(_config.Key);

        var securityKey = new RsaSecurityKey(rsaKey)
        {
            KeyId = _config.KeyId
        };

        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

        var now = DateTime.UtcNow;
        var effectiveUserId = _config.GetUserId();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, effectiveUserId),
            new(JwtRegisteredClaimNames.Iss, effectiveUserId),
            new(JwtRegisteredClaimNames.Aud, authority),
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
        return tokenHandler.WriteToken(token);
    }

    private static string NormalizeAuthority(string authority)
    {
        if (string.IsNullOrWhiteSpace(authority))
        {
            throw new InvalidOperationException("ZITADEL authority must be provided for JWT authentication.");
        }

        if (!Uri.TryCreate(authority, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException($"Invalid ZITADEL authority '{authority}'. Provide an absolute HTTPS URL.");
        }

        if (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("JWT Profile authentication requires an HTTPS authority.");
        }

        return baseUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private static string BuildTokenEndpoint(string normalizedAuthority)
    {
        var baseUri = new Uri(normalizedAuthority, UriKind.Absolute);
        var endpointUri = new Uri(baseUri, "oauth/v2/token");
        return endpointUri.ToString();
    }
}
