using Duende.AspNetCore.Authentication.OAuth2Introspection;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.Json;
using ZitadelSDK.Authentication;

namespace ZitadelSDK.Extensions;

/// <summary>
/// Extension methods for configuring ZITADEL authentication.
/// </summary>
public static class ZitadelAuthenticationExtensions
{
    /// <summary>
    /// Add the ZITADEL introspection handler without caring for session handling. 
    /// This is typically used by web APIs that only need to verify the access token that is presented. 
    /// This handler can manage JWT as well as opaque access tokens.
    /// </summary>
    /// <param name="builder">The AuthenticationBuilder to configure.</param>
    /// <param name="configureOptions">An optional action to configure the ZITADEL handler options.</param>
    /// <returns>The configured AuthenticationBuilder.</returns>
    public static AuthenticationBuilder AddZitadelIntrospection(
        this AuthenticationBuilder builder,
        Action<ZitadelIntrospectionOptions>? configureOptions = null)
    {
        return builder.AddZitadelIntrospection("ZITADEL", configureOptions);
    }

    /// <summary>
    /// Add the ZITADEL introspection handler with a specific authentication scheme.
    /// </summary>
    /// <param name="builder">The AuthenticationBuilder to configure.</param>
    /// <param name="authenticationScheme">The authentication scheme name.</param>
    /// <param name="configureOptions">An optional action to configure the ZITADEL handler options.</param>
    /// <returns>The configured AuthenticationBuilder.</returns>
    public static AuthenticationBuilder AddZitadelIntrospection(
        this AuthenticationBuilder builder,
        string authenticationScheme,
        Action<ZitadelIntrospectionOptions>? configureOptions = null)
    {
        return builder.AddOAuth2Introspection(authenticationScheme, options =>
        {
            // Create ZITADEL-specific options with defaults
            var zitadelOptions = new ZitadelIntrospectionOptions
            {
                ClientCredentialStyle = ClientCredentialStyle.AuthorizationHeader,
                AuthorizationHeaderStyle = BasicAuthenticationHeaderStyle.Rfc6749,
                RoleClaimType = ZitadelClaimTypes.Role
            };

            // Apply user configuration
            configureOptions?.Invoke(zitadelOptions);

            // Validate required options
            if (string.IsNullOrWhiteSpace(zitadelOptions.Authority))
            {
                throw new InvalidOperationException("ZITADEL Authority must be configured.");
            }

            // Copy all options to OAuth2IntrospectionOptions
            options.Authority = zitadelOptions.Authority;
            options.Events = zitadelOptions.Events;
            options.AuthenticationType = zitadelOptions.AuthenticationType;
            options.CacheDuration = zitadelOptions.CacheDuration;
            options.ClientId = zitadelOptions.ClientId;
            options.ClientSecret = zitadelOptions.ClientSecret;
            options.DiscoveryPolicy = zitadelOptions.DiscoveryPolicy;
            options.EnableCaching = zitadelOptions.EnableCaching;
            options.IntrospectionEndpoint = zitadelOptions.IntrospectionEndpoint;
            options.SaveToken = zitadelOptions.SaveToken;
            options.TokenRetriever = zitadelOptions.TokenRetriever;
            options.AuthorizationHeaderStyle = zitadelOptions.AuthorizationHeaderStyle;
            options.CacheKeyGenerator = zitadelOptions.CacheKeyGenerator;
            options.CacheKeyPrefix = zitadelOptions.CacheKeyPrefix;
            options.ClientCredentialStyle = zitadelOptions.ClientCredentialStyle;
            options.NameClaimType = zitadelOptions.NameClaimType;
            options.RoleClaimType = zitadelOptions.RoleClaimType;
            options.TokenTypeHint = zitadelOptions.TokenTypeHint;
            options.SkipTokensWithDots = zitadelOptions.SkipTokensWithDots;
            options.ClaimsIssuer = zitadelOptions.ClaimsIssuer;
            options.EventsType = zitadelOptions.EventsType;
            options.ForwardAuthenticate = zitadelOptions.ForwardAuthenticate;
            options.ForwardChallenge = zitadelOptions.ForwardChallenge;
            options.ForwardDefault = zitadelOptions.ForwardDefault;
            options.ForwardForbid = zitadelOptions.ForwardForbid;
            options.ForwardDefaultSelector = zitadelOptions.ForwardDefaultSelector;
            options.ForwardSignIn = zitadelOptions.ForwardSignIn;
            options.ForwardSignOut = zitadelOptions.ForwardSignOut;

            // Configure role claim transformation
            options.Events.OnTokenValidated += context =>
            {
                var roleClaims = context.Principal?.Claims.Where(c => c.Type == context.Options.RoleClaimType);
                if (roleClaims is null)
                {
                    return Task.CompletedTask;
                }

                var claims = new List<Claim>();

                foreach (var roleClaim in roleClaims)
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(roleClaim.Value);
                        if (parsed is null)
                        {
                            continue;
                        }

                        foreach (var (orgId, roles) in parsed)
                        {
                            foreach (var role in roles.Keys)
                            {
                                claims.Add(new Claim(
                                    ZitadelClaimTypes.OrganizationRole(orgId),
                                    role,
                                    ClaimValueTypes.String,
                                    context.Options.ClaimsIssuer));

                                claims.Add(new Claim(
                                    ClaimTypes.Role,
                                    role,
                                    ClaimValueTypes.String,
                                    context.Options.ClaimsIssuer));
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // Ignore malformed role claims to avoid interrupting the pipeline
                        continue;
                    }
                }

                if (claims.Count != 0)
                {
                    context.Principal?.AddIdentity(new ClaimsIdentity(claims));
                }

                return Task.CompletedTask;
            };

            // Configure JWT profile authentication if provided
            if (zitadelOptions.JwtProfile != null)
            {
                options.ClientId = null;
                options.ClientSecret = null;
                options.ClientCredentialStyle = ClientCredentialStyle.PostBody;
                options.Events.OnUpdateClientAssertion += async context =>
                {
                    var jwt = await zitadelOptions.JwtProfile.GetSignedJwtAsync(options.Authority!);
                    context.ClientAssertion = new ClientAssertion
                    {
                        Type = ZitadelIntrospectionOptions.JwtBearerClientAssertionType,
                        Value = jwt
                    };
                    context.ClientAssertionExpirationTime = DateTime.UtcNow.AddMinutes(4);
                };
            }
        });
    }

    /// <summary>
    /// Add ZITADEL JWT Bearer authentication for validating JWT tokens.
    /// </summary>
    /// <param name="builder">The AuthenticationBuilder to configure.</param>
    /// <param name="configureOptions">An optional action to configure the ZITADEL JWT Bearer options.</param>
    /// <returns>The configured AuthenticationBuilder.</returns>
    public static AuthenticationBuilder AddZitadelJwtBearer(
        this AuthenticationBuilder builder,
        Action<ZitadelJwtBearerOptions>? configureOptions = null)
    {
        return builder.AddZitadelJwtBearer(JwtBearerDefaults.AuthenticationScheme, configureOptions);
    }

    /// <summary>
    /// Add ZITADEL JWT Bearer authentication with a specific authentication scheme.
    /// </summary>
    /// <param name="builder">The AuthenticationBuilder to configure.</param>
    /// <param name="authenticationScheme">The authentication scheme name.</param>
    /// <param name="configureOptions">An optional action to configure the ZITADEL JWT Bearer options.</param>
    /// <returns>The configured AuthenticationBuilder.</returns>
    public static AuthenticationBuilder AddZitadelJwtBearer(
        this AuthenticationBuilder builder,
        string authenticationScheme,
        Action<ZitadelJwtBearerOptions>? configureOptions = null)
    {
        return builder.AddJwtBearer(authenticationScheme, options =>
        {
            // Create ZITADEL-specific options with defaults
            var zitadelOptions = new ZitadelJwtBearerOptions();

            // Apply user configuration
            configureOptions?.Invoke(zitadelOptions);

            // Validate required options
            if (string.IsNullOrWhiteSpace(zitadelOptions.Authority))
            {
                throw new InvalidOperationException("ZITADEL Authority must be configured.");
            }

            // Configure JWT Bearer options
            options.Authority = zitadelOptions.Authority;
            options.RequireHttpsMetadata = zitadelOptions.RequireHttpsMetadata;
            options.SaveToken = zitadelOptions.SaveToken;

            // Configure token validation to work with ZITADEL's token format
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = zitadelOptions.ValidateIssuer,
                ValidateLifetime = zitadelOptions.ValidateLifetime,
                ClockSkew = zitadelOptions.ClockSkew,
                NameClaimType = zitadelOptions.NameClaimType,
                RoleClaimType = zitadelOptions.RoleClaimType,
                ValidateAudience = zitadelOptions.ValidateAudience
            };

            // Configure audience - ZITADEL tokens may have multiple audiences in an array
            if (zitadelOptions.Audiences != null && zitadelOptions.Audiences.Any())
            {
                // Multiple audiences provided - use ValidAudiences to support array-based aud claims
                options.TokenValidationParameters.ValidAudiences = zitadelOptions.Audiences;
            }
            else if (!string.IsNullOrWhiteSpace(zitadelOptions.Audience))
            {
                // Single audience provided - wrap in array to support ZITADEL's array-based aud claims
                options.TokenValidationParameters.ValidAudiences = [zitadelOptions.Audience];
            }
            // If no audience specified, ValidateAudience setting will control whether to validate

            // Configure role claim transformation
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    // Transform ZITADEL role claims
                    var roleClaims = context.Principal?.Claims
                        .Where(c => c.Type == zitadelOptions.RoleClaimType)
                        .ToList();

                    if (roleClaims != null && roleClaims.Count != 0)
                    {
                        var newClaims = new List<Claim>();

                        foreach (var roleClaim in roleClaims)
                        {
                            try
                            {
                                // Parse ZITADEL role structure: {"org-id": {"role-key": "role-name"}}
                                var roleDict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(roleClaim.Value);
                                if (roleDict != null)
                                {
                                    foreach (var org in roleDict)
                                    {
                                        foreach (var role in org.Value)
                                        {
                                            // Add organization-specific role
                                            newClaims.Add(new Claim(
                                                ZitadelClaimTypes.OrganizationRole(org.Key),
                                                role.Key,
                                                ClaimValueTypes.String,
                                                context.Options.ClaimsIssuer));

                                            // Add standard role claim
                                            newClaims.Add(new Claim(
                                                ClaimTypes.Role,
                                                role.Key,
                                                ClaimValueTypes.String,
                                                context.Options.ClaimsIssuer));
                                        }
                                    }
                                }
                            }
                            catch (JsonException)
                            {
                                // If parsing fails, skip this claim
                                continue;
                            }
                        }

                        if (newClaims.Count != 0)
                        {
                            var identity = new ClaimsIdentity(newClaims);
                            context.Principal?.AddIdentity(identity);
                        }
                    }

                    return Task.CompletedTask;
                }
            };
        });
    }
}
