using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;
using ZitadelSDK.Extensions;
using ZitadelSDK.UnitTests.TestHelpers;

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

    [Fact]
    public async Task CheckHealth_WithNonHttpsAuthority_ReturnsUnhealthy()
    {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient().Returns(new HttpClient());

        var healthCheck = new ZitadelHealthCheck("http://example.com", httpClientFactory);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
        Assert.Contains("must use HTTPS", result.Exception!.Message);
    }

    [Fact]
    public async Task CheckHealth_WithHttpAuthority_AllowsReadyCheckWhenInsecureTransportIsEnabled()
    {
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient().Returns(new HttpClient(handler));

        var healthCheck = new ZitadelHealthCheck(
            "http://example.com/base-path",
            httpClientFactory,
            allowInsecureTransport: true);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("http://example.com/debug/ready", handler.LastRequest?.RequestUri?.ToString());
    }
}
