using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ZitadelSDK.Services;

/// <summary>
/// Extension methods for adding ZITADEL SDK services to the dependency injection container.
/// </summary>
public static class ZitadelServiceCollectionExtensions
{
    /// <summary>
    /// Adds the ZITADEL SDK to the service collection and returns a builder for configuring authentication.
    /// Use .WithJwtAuth() or .WithPatAuth() to configure authentication.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration containing ZITADEL settings.</param>
    /// <returns>A builder for configuring ZITADEL authentication.</returns>
    public static ZitadelSdkBuilder AddZitadelSdk(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var builder = services.AddOptions<ZitadelClientOptions>()
            .Bind(configuration.GetSection(ZitadelClientOptions.SectionName));

        ConfigureValidation(builder);
        RegisterSdk(services);

        return new ZitadelSdkBuilder(services);
    }

    /// <summary>
    /// Adds the ZITADEL SDK to the service collection with custom configuration and returns a builder for configuring authentication.
    /// Use .WithJwtAuth() or .WithPatAuth() to configure authentication.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure ZITADEL options.</param>
    /// <returns>A builder for configuring ZITADEL authentication.</returns>
    public static ZitadelSdkBuilder AddZitadelSdk(this IServiceCollection services, Action<ZitadelClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = services.AddOptions<ZitadelClientOptions>();
        builder.Configure(configure);

        ConfigureValidation(builder);
        RegisterSdk(services);

        return new ZitadelSdkBuilder(services);
    }

    private static void ConfigureValidation(OptionsBuilder<ZitadelClientOptions> builder)
    {
        builder
            .Validate(options => !string.IsNullOrWhiteSpace(options.Authority),
                "Zitadel authority must be provided.")
            .ValidateOnStart();
    }

    private static void RegisterSdk(IServiceCollection services)
    {
        services.TryAddSingleton<IZitadelSdk, ZitadelSdk>();
    }
}
