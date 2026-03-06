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
        var token = Options.TokenRetriever?.Invoke(Request) ?? RetrieveTokenFromAuthorizationHeader(Request);
        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.NoResult();
        }

        if (Options.SkipTokensWithDots && token.Contains('.'))
        {
            return AuthenticateResult.NoResult();
        }

        var cacheKey = GetCacheKey(token);
        if (Options.EnableCaching && _memoryCache.TryGetValue<AuthenticationTicket>(cacheKey, out var cachedTicket))
        {
            return AuthenticateResult.Success(cachedTicket!);
        }

        try
        {
            var introspectionJson = await IntrospectAsync(token);
            if (!introspectionJson.TryGetProperty("active", out var activeElement) || !activeElement.GetBoolean())
            {
                return AuthenticateResult.Fail("Token is inactive.");
            }

            var principal = BuildPrincipal(introspectionJson);

            var validatedContext = new ZitadelTokenValidatedContext(Context, Scheme, Options)
            {
                Token = token,
                Principal = principal
            };

            await Options.Events.OnTokenValidated(validatedContext);

            if (validatedContext.Principal is null)
            {
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
            }

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception exception)
        {
            var failedContext = new ZitadelIntrospectionFailedContext(Context, Scheme, Options, exception);
            await Options.Events.OnAuthenticationFailed(failedContext);
            if (failedContext.Result != null)
            {
                return failedContext.Result;
            }

            return AuthenticateResult.Fail(exception);
        }
    }

    private async Task<JsonElement> IntrospectAsync(string token)
    {
        var endpoint = GetIntrospectionEndpoint();
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

        request.Content = new FormUrlEncodedContent(form);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, Context.RequestAborted);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Introspection endpoint returned status {(int)response.StatusCode}.");
        }

        var content = await response.Content.ReadAsStringAsync(Context.RequestAborted);
        using var document = JsonDocument.Parse(content);
        return document.RootElement.Clone();
    }

    private async Task AddClientAuthenticationAsync(HttpRequestMessage request, List<KeyValuePair<string, string>> form)
    {
        if (Options.JwtProfile != null)
        {
            var assertion = await Options.JwtProfile.GetSignedJwtAsync(Options.Authority!);

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
            return;
        }

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
            if (!Uri.TryCreate(Options.IntrospectionEndpoint, UriKind.Absolute, out var introspectionUri))
            {
                throw new InvalidOperationException("ZITADEL IntrospectionEndpoint must be an absolute HTTPS URL.");
            }

            if (!string.Equals(introspectionUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("ZITADEL IntrospectionEndpoint must use HTTPS.");
            }

            return introspectionUri.ToString();
        }

        if (string.IsNullOrWhiteSpace(Options.Authority))
        {
            throw new InvalidOperationException("ZITADEL Authority must be configured.");
        }

        if (!Uri.TryCreate(Options.Authority, UriKind.Absolute, out var authorityUri))
        {
            throw new InvalidOperationException("ZITADEL Authority must be an absolute HTTPS URL.");
        }

        if (!string.Equals(authorityUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("ZITADEL Authority must use HTTPS.");
        }

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
}
