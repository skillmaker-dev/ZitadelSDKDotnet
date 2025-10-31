using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using ZitadelSDK.Authentication;
using ZitadelSDK.Services;

namespace ZitadelSDK.UnitTests.Services;

public class ZitadelServiceCollectionExtensionsTests
{
    [Fact]
    public void AddZitadelSdk_WithConfiguration_BindsOptionsAndRegistersSdk()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceAdmin:Authority"] = "https://example.com",
                ["ServiceAdmin:AuthenticationType"] = "Custom"
            })
            .Build();

        // Act
        var builder = services.AddZitadelSdk(configuration);

        // Assert
        Assert.NotNull(builder);

        var descriptor = Assert.Single(services.Where(d => d.ServiceType == typeof(IZitadelSdk)));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);

        var credentialProvider = Substitute.For<IZitadelCredentialProvider>();
        credentialProvider
            .CreateCallCredentials(Arg.Any<string>())
            .Returns(CallCredentials.FromInterceptor((_, _) => Task.CompletedTask));

        services.AddSingleton(credentialProvider);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ZitadelClientOptions>>().Value;

        Assert.Equal("https://example.com", options.Authority);
        Assert.Equal("Custom", options.AuthenticationType);

        var sdk = provider.GetRequiredService<IZitadelSdk>();
        Assert.NotNull(sdk);
    }

    [Fact]
    public void AddZitadelSdk_WithConfigureAction_UsesProvidedOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddZitadelSdk(options =>
        {
            options.Authority = "https://unit.test";
            options.AuthenticationType = "Token";
        });

        var credentialProvider = Substitute.For<IZitadelCredentialProvider>();
        credentialProvider
            .CreateCallCredentials(Arg.Any<string>())
            .Returns(CallCredentials.FromInterceptor((_, _) => Task.CompletedTask));

        services.AddSingleton(credentialProvider);

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ZitadelClientOptions>>().Value;
        Assert.Equal("https://unit.test", options.Authority);
        Assert.Equal("Token", options.AuthenticationType);
    }

    [Fact]
    public void AddZitadelSdk_MissingAuthority_ThrowsValidationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        services.AddZitadelSdk(configuration);

        services.AddSingleton(Substitute.For<IZitadelCredentialProvider>());

        using var provider = services.BuildServiceProvider();

        // Act & Assert
        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ZitadelClientOptions>>().Value);
    }
}
