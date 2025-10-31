using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ZitadelSDK.Extensions;
using ZitadelSDK.Services;

namespace ZitadelSDK.UnitTests.Extensions;

public class ZitadelClientExtensionsTests
{
    [Fact]
    public void AddZitadelClient_ServiceCollection_ResolvesFromSdk()
    {
        // Arrange
        var services = new ServiceCollection();
        var sdk = Substitute.For<IZitadelSdk>();
        var callInvoker = Substitute.For<CallInvoker>();
        var client = Substitute.For<TestGrpcClient>(callInvoker);

        sdk.GetClient<TestGrpcClient>().Returns(client);

        services.AddSingleton(sdk);

        // Act
        services.AddZitadelClient<TestGrpcClient>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Assert
        var resolved = scope.ServiceProvider.GetRequiredService<TestGrpcClient>();
        Assert.Same(client, resolved);
    }

    [Fact]
    public void AddZitadelClients_ServiceCollection_ThrowsForNonGrpcType()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => services.AddZitadelClients(ServiceLifetime.Singleton, typeof(string)));
    }

    [Fact]
    public void AddZitadelClient_Builder_ForwardsToServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddZitadelSdk(options =>
        {
            options.Authority = "https://example.com";
        });

        var sdk = Substitute.For<IZitadelSdk>();
        var callInvoker = Substitute.For<CallInvoker>();
        var client = Substitute.For<TestGrpcClient>(callInvoker);
        sdk.GetClient<TestGrpcClient>().Returns(client);

        var registeredDescriptor = services.Single(sd => sd.ServiceType == typeof(IZitadelSdk));
        services.Remove(registeredDescriptor);
        services.AddSingleton(sdk);

        // Act
        builder.AddZitadelClient<TestGrpcClient>();

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<TestGrpcClient>();

        // Assert
        Assert.Same(client, resolved);
    }

    public class TestGrpcClient : ClientBase<TestGrpcClient>
    {
        public TestGrpcClient(CallInvoker callInvoker) : base(callInvoker)
        {
        }

        private TestGrpcClient(ClientBaseConfiguration configuration) : base(configuration)
        {
        }

        protected override TestGrpcClient NewInstance(ClientBaseConfiguration configuration)
        {
            return new TestGrpcClient(configuration);
        }
    }
}
