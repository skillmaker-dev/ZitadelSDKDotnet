using Microsoft.Extensions.Configuration;
using ZitadelSDK.Services;

namespace ZitadelSDK.Extensions;

/// <summary>
/// Extension methods for configuring ZITADEL SDK builder.
/// Provides convenient helpers for loading configuration from IConfiguration.
/// </summary>
public static class ZitadelSdkBuilderExtensions
{
    /// <summary>
    /// Configures JWT Profile authentication using settings from configuration.
    /// Expects configuration section with KeyId, Key, and UserId properties.
    /// </summary>
    /// <param name="builder">The ZITADEL SDK builder.</param>
    /// <param name="configuration">The configuration containing JWT Profile settings.</param>
    /// <param name="sectionName">The configuration section name (default: "ServiceAdmin:JwtProfile").</param>
    /// <returns>The builder for method chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddZitadelSdk(builder.Configuration)
    ///     .WithJwtAuth(builder.Configuration);
    /// </code>
    /// </example>
    public static ZitadelSdkBuilder WithJwtAuth(
        this ZitadelSdkBuilder builder,
        IConfiguration configuration,
        string sectionName = "ServiceAdmin:JwtProfile")
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var jwtProfileSection = configuration.GetSection(sectionName);
        if (!jwtProfileSection.Exists())
        {
            throw new InvalidOperationException(
                $"JWT Profile configuration section '{sectionName}' not found in configuration.");
        }

        return builder.WithJwtAuth(config =>
        {
            jwtProfileSection.Bind(config);
        });
    }

    /// <summary>
    /// Configures Personal Access Token authentication using a token from configuration.
    /// </summary>
    /// <param name="builder">The ZITADEL SDK builder.</param>
    /// <param name="configuration">The configuration containing the PAT.</param>
    /// <param name="configurationKey">The configuration key for the PAT (default: "ServiceAdmin:PersonalAccessToken").</param>
    /// <returns>The builder for method chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddZitadelSdk(builder.Configuration)
    ///     .WithPatAuth(builder.Configuration);
    /// </code>
    /// </example>
    public static ZitadelSdkBuilder WithPatAuth(
        this ZitadelSdkBuilder builder,
        IConfiguration configuration,
        string configurationKey = "ServiceAdmin:PersonalAccessToken")
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var token = configuration[configurationKey];
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                $"Personal Access Token not found at configuration key '{configurationKey}'.");
        }

        return builder.WithPatAuth(token);
    }
}
