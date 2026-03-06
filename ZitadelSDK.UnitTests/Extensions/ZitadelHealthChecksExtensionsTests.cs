using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ZitadelSDK.Extensions;

namespace ZitadelSDK.UnitTests.Extensions;

public class ZitadelHealthChecksExtensionsTests
{
    [Fact]
    public void AddZitadel_DoesNotRequireHttpClientFactoryAtRegistrationTime()
    {
        var services = new ServiceCollection();
        var builder = services.AddHealthChecks();

        var exception = Record.Exception(() => builder.AddZitadel("https://example.com"));

        Assert.Null(exception);
    }

    [Fact]
    public void AddZitadel_RegistersHealthCheckWithProvidedName()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();

        services.AddHealthChecks()
            .AddZitadel("https://example.com", name: "zitadel-custom");

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        var registration = Assert.Single(options.Registrations, x => x.Name == "zitadel-custom");
        Assert.NotNull(registration.Factory);
    }
}
