using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ZitadelSDK.Internal;

namespace ZitadelSDK.Authentication;

internal sealed class ZitadelIntrospectionHandler(
    IOptionsMonitor<ZitadelIntrospectionOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IHttpClientFactory httpClientFactory,
    IMemoryCache memoryCache) : AuthenticationHandler<ZitadelIntrospectionOptions>(options, logger, encoder)
{
    private const string HttpClientName = "ZitadelSDK.Introspection";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IMemoryCache _memoryCache = memoryCache;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Logger.LogDebug(
            "Introspection: Starting authentication for request {Path}",
            SanitizeForLog(Request.Path));

        var token = Options.TokenRetriever?.Invoke(Request) ?? RetrieveTokenFromAuthorizationHeader(Request);
        if (string.IsNullOrWhiteSpace(token))
        {
            Logger.LogDebug("Introspection: No bearer token found in request.");
            return AuthenticateResult.NoResult();
        }

        Logger.LogDebug("Introspection: Token found (length={TokenLength}).", token.Length);

        if (Options.SkipTokensWithDots && token.Contains('.'))
        {
            Logger.LogDebug("Introspection: Skipping token with dots (SkipTokensWithDots=true).");
            return AuthenticateResult.NoResult();
        }

        var cacheKey = GetCacheKey(token);
        if (Options.EnableCaching)
        {
            if (_memoryCache.TryGetValue<AuthenticationTicket>(cacheKey, out var cachedTicket))
            {
                Logger.LogDebug("Introspection: Cache hit for token.");
                return AuthenticateResult.Success(cachedTicket!);
            }

            Logger.LogInformation("Introspection: Cache miss. Calling introspection endpoint. Authority={Authority}, JwtProfile={HasJwtProfile}, ClientId={ClientId}",
                Options.Authority, Options.JwtProfile != null, Options.ClientId ?? "(null)");
        }
        else
        {
            Logger.LogDebug("Introspection: Caching disabled. Calling introspection endpoint. Authority={Authority}, JwtProfile={HasJwtProfile}, ClientId={ClientId}",
                Options.Authority, Options.JwtProfile != null, Options.ClientId ?? "(null)");
        }

        try
        {
            var introspectionJson = await IntrospectWithRetryOnInactiveAsync(token);

            if (!IsTokenActive(introspectionJson))
            {
                Logger.LogDebug("Introspection: Token is inactive after retry policy exhausted.");
                return AuthenticateResult.Fail("Token is inactive.");
            }

            Logger.LogDebug("Introspection: Token is active. Validating claims.");

            // Validate issuer matches the configured authority (defense-in-depth)
            if (!string.IsNullOrWhiteSpace(Options.Authority) &&
                introspectionJson.TryGetProperty("iss", out var issElement) &&
                issElement.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var issuer = issElement.GetString();
                var expectedIssuer = Options.Authority.TrimEnd('/');
                if (!string.Equals(issuer?.TrimEnd('/'), expectedIssuer, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogWarning("Introspection: Issuer mismatch. Token iss={Issuer}, expected={Expected}", issuer, expectedIssuer);
                    return AuthenticateResult.Fail(
                        $"Token issuer '{issuer}' does not match expected authority '{expectedIssuer}'.");
                }
            }

            var principal = BuildPrincipal(introspectionJson);
            Logger.LogDebug("Introspection: Principal built with {ClaimCount} claims.", principal.Claims.Count());

            var validatedContext = new ZitadelTokenValidatedContext(Context, Scheme, Options)
            {
                Token = token,
                Principal = principal
            };

            await Options.Events.OnTokenValidated(validatedContext);

            if (validatedContext.Principal is null)
            {
                Logger.LogWarning("Introspection: Principal was nullified by OnTokenValidated event.");
                return AuthenticateResult.Fail("No principal produced from token introspection.");
            }

            var properties = new AuthenticationProperties();
            if (Options.SaveToken)
            {
                properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = token }]);
            }

            var ticket = new AuthenticationTicket(validatedContext.Principal, properties, Scheme.Name);

            if (Options.EnableCaching)
            {
                _memoryCache.Set(cacheKey, ticket, Options.CacheDuration);
                Logger.LogDebug("Introspection: Result cached for {CacheDuration}.", Options.CacheDuration);
            }

            Logger.LogInformation("Introspection: Authentication succeeded.");
            return AuthenticateResult.Success(ticket);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Introspection: Authentication failed with exception.");
            var failedContext = new ZitadelIntrospectionFailedContext(Context, Scheme, Options, exception);
            await Options.Events.OnAuthenticationFailed(failedContext);
            if (failedContext.Result != null)
            {
                return failedContext.Result;
            }

            return AuthenticateResult.Fail(exception);
        }
    }

    private async Task<JsonElement> IntrospectWithRetryOnInactiveAsync(string token)
    {
        var attempts = 0;

        while (true)
        {
            var introspectionJson = await IntrospectAsync(token);
            if (IsTokenActive(introspectionJson))
            {
                return introspectionJson;
            }

            if (attempts >= Options.InactiveTokenRetryCount)
            {
                return introspectionJson;
            }

            attempts++;

            if (Options.InactiveTokenRetryDelay > TimeSpan.Zero)
            {
                Logger.LogDebug(
                    "Introspection: Token inactive on attempt {Attempt}. Retrying in {RetryDelay}.",
                    attempts,
                    Options.InactiveTokenRetryDelay);

                await Task.Delay(Options.InactiveTokenRetryDelay, Context.RequestAborted);
            }
        }
    }

    private static bool IsTokenActive(JsonElement introspectionJson)
    {
        return introspectionJson.TryGetProperty("active", out var activeElement)
            && activeElement.ValueKind == JsonValueKind.True;
    }

    private async Task<JsonElement> IntrospectAsync(string token)
    {
        var endpoint = GetIntrospectionEndpoint();
        Logger.LogDebug("Introspection: POST to {Endpoint}", endpoint);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);

        var form = new List<KeyValuePair<string, string>>
        {
            new("token", token)
        };

        if (!string.IsNullOrWhiteSpace(Options.TokenTypeHint))
        {
            form.Add(new KeyValuePair<string, string>("token_type_hint", Options.TokenTypeHint));
        }

        await AddClientAuthenticationAsync(request, form);

        Logger.LogDebug("Introspection: Sending {FieldCount} form fields: {FieldNames}",
            form.Count, string.Join(", ", form.Select(f => f.Key)));

        request.Content = new FormUrlEncodedContent(form);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, Context.RequestAborted);

        Logger.LogDebug("Introspection: Response status {StatusCode}", (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(Context.RequestAborted);
            Logger.LogWarning(
                "Introspection endpoint returned {StatusCode}. Response: {ErrorBody}",
                (int)response.StatusCode, errorBody);
            throw new InvalidOperationException(
                $"Introspection endpoint returned status {(int)response.StatusCode}. Response: {errorBody}");
        }

        var content = await response.Content.ReadAsStringAsync(Context.RequestAborted);
        using var document = JsonDocument.Parse(content);
        return document.RootElement.Clone();
    }

    private async Task AddClientAuthenticationAsync(HttpRequestMessage request, List<KeyValuePair<string, string>> form)
    {
        if (Options.JwtProfile != null)
        {
            Logger.LogDebug("Using JWT Profile for introspection client authentication.");
            var assertion = await Options.JwtProfile.GetSignedJwtAsync(
                Options.Authority!,
                Options.AllowInsecureTransport);

            var updateContext = new ZitadelUpdateClientAssertionContext(Context, Scheme, Options)
            {
                ClientAssertion = assertion,
                ClientAssertionType = ZitadelIntrospectionOptions.JwtBearerClientAssertionType,
                ClientAssertionExpirationTime = DateTimeOffset.UtcNow.AddMinutes(4)
            };

            await Options.Events.OnUpdateClientAssertion(updateContext);

            if (!string.IsNullOrWhiteSpace(Options.ClientId))
            {
                form.Add(new KeyValuePair<string, string>("client_id", Options.ClientId));
            }

            form.Add(new KeyValuePair<string, string>("client_assertion_type", updateContext.ClientAssertionType!));
            form.Add(new KeyValuePair<string, string>("client_assertion", updateContext.ClientAssertion!));
            return;
        }

        if (string.IsNullOrWhiteSpace(Options.ClientId))
        {
            Logger.LogWarning(
                "No client authentication configured for introspection. " +
                "JwtProfile is null and ClientId is empty. " +
                "The introspection endpoint will likely reject this request.");
            return;
        }

        Logger.LogDebug("Using ClientId/ClientSecret for introspection client authentication.");

        if (Options.ClientCredentialStyle == ClientCredentialStyle.PostBody)
        {
            form.Add(new KeyValuePair<string, string>("client_id", Options.ClientId));
            if (!string.IsNullOrWhiteSpace(Options.ClientSecret))
            {
                form.Add(new KeyValuePair<string, string>("client_secret", Options.ClientSecret));
            }

            return;
        }

        var id = Options.ClientId;
        var secret = Options.ClientSecret ?? string.Empty;

        string credential;
        if (Options.AuthorizationHeaderStyle == BasicAuthenticationHeaderStyle.Rfc6749)
        {
            credential = $"{Uri.EscapeDataString(id)}:{Uri.EscapeDataString(secret)}";
        }
        else
        {
            credential = $"{id}:{secret}";
        }

        var value = Convert.ToBase64String(Encoding.UTF8.GetBytes(credential));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", value);
    }

    private ClaimsPrincipal BuildPrincipal(JsonElement introspectionJson)
    {
        var claims = new List<Claim>();

        foreach (var property in introspectionJson.EnumerateObject())
        {
            if (property.NameEquals("active"))
            {
                continue;
            }

            AddClaims(claims, property.Name, property.Value);
        }

        var identity = new ClaimsIdentity(claims, Options.AuthenticationType ?? Scheme.Name, Options.NameClaimType, Options.RoleClaimType);
        return new ClaimsPrincipal(identity);
    }

    private static void AddClaims(List<Claim> claims, string claimType, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    AddClaims(claims, claimType, item);
                }
                break;
            case JsonValueKind.Object:
                claims.Add(new Claim(claimType, value.GetRawText(), ClaimValueTypes.String));
                break;
            case JsonValueKind.String:
                claims.Add(new Claim(claimType, value.GetString()!));
                break;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                claims.Add(new Claim(claimType, value.ToString(), ClaimValueTypes.String));
                break;
        }
    }

    private string GetIntrospectionEndpoint()
    {
        if (!string.IsNullOrWhiteSpace(Options.IntrospectionEndpoint))
        {
            var introspectionUri = TransportSecurity.ValidateUri(
                Options.IntrospectionEndpoint,
                "ZITADEL IntrospectionEndpoint",
                Options.AllowInsecureTransport);

            return introspectionUri.ToString();
        }

        if (string.IsNullOrWhiteSpace(Options.Authority))
        {
            throw new InvalidOperationException("ZITADEL Authority must be configured.");
        }

        var authorityUri = TransportSecurity.ValidateUri(
            Options.Authority,
            "ZITADEL Authority",
            Options.AllowInsecureTransport);

        var introspectionUriFromAuthority = new Uri(authorityUri, "/oauth/v2/introspect");
        return introspectionUriFromAuthority.ToString();
    }

    private string GetCacheKey(string token)
    {
        if (Options.CacheKeyGenerator != null)
        {
            return Options.CacheKeyGenerator(token);
        }

        var hashedToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        return $"{Options.CacheKeyPrefix}{hashedToken}";
    }

    private static string? RetrieveTokenFromAuthorizationHeader(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Authorization", out var authorizationValues))
        {
            return null;
        }

        var authorization = authorizationValues.ToString();
        const string bearerPrefix = "Bearer ";
        if (!authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return authorization[bearerPrefix.Length..].Trim();
    }

    private static string SanitizeForLog(PathString path)
    {
        return (path.Value ?? string.Empty)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }
}
