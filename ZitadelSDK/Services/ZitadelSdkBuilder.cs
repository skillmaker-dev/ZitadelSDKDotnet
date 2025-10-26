using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZitadelSDK.Authentication;

namespace ZitadelSDK.Services;

/// <summary>
/// Builder for configuring the ZITADEL SDK with fluent API.
/// </summary>
public class ZitadelSdkBuilder
{
    private readonly IServiceCollection _services;

    internal ZitadelSdkBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Gets the underlying service collection to allow further service registrations.
    /// </summary>
    public IServiceCollection Services => _services;

    /// <summary>
    /// Configures JWT Profile authentication for the ZITADEL SDK.
    /// </summary>
    /// <param name="configure">Action to configure JWT Profile settings.</param>
    /// <returns>The builder for method chaining.</returns>
    public ZitadelSdkBuilder WithJwtAuth(Action<JwtProfileConfig> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var jwtConfig = new JwtProfileConfig();
        configure(jwtConfig);

        if (!jwtConfig.IsValid())
        {
            throw new InvalidOperationException(
                "JWT Profile configuration is invalid. KeyId, Key, and UserId are required.");
        }

        // Register the credential provider as a singleton
        _services.AddSingleton<IZitadelCredentialProvider>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<ILogger<JwtProfileCredentialProvider>>();
            var options = sp.GetRequiredService<IOptions<ZitadelClientOptions>>();

            return new JwtProfileCredentialProvider(
                jwtConfig,
                httpClientFactory,
                logger,
                options.Value.AuthenticationType);
        });

        return this;
    }

    /// <summary>
    /// Configures Personal Access Token authentication for the ZITADEL SDK.
    /// </summary>
    /// <param name="token">The personal access token.</param>
    /// <returns>The builder for method chaining.</returns>
    public ZitadelSdkBuilder WithPatAuth(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Personal access token cannot be null or empty.", nameof(token));
        }

        _services.AddSingleton<IZitadelCredentialProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ZitadelClientOptions>>();
            return new PersonalAccessTokenCredentialProvider(
                token,
                options.Value.AuthenticationType);
        });

        return this;
    }

    /// <summary>
    /// Configures Personal Access Token authentication for the ZITADEL SDK using a configuration action.
    /// </summary>
    /// <param name="configure">Action to retrieve the personal access token (e.g., from configuration).</param>
    /// <returns>The builder for method chaining.</returns>
    public ZitadelSdkBuilder WithPatAuth(Func<IServiceProvider, string> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        _services.AddSingleton<IZitadelCredentialProvider>(sp =>
        {
            var token = configure(sp);

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("Personal access token cannot be null or empty.");
            }

            var options = sp.GetRequiredService<IOptions<ZitadelClientOptions>>();
            return new PersonalAccessTokenCredentialProvider(
                token,
                options.Value.AuthenticationType);
        });

        return this;
    }
}
