using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using ZitadelSDK.Authentication;
using ZitadelSDK.Extensions;
using ZitadelSDK.Services;
using ZitadelSDK.UnitTests.TestHelpers;

namespace ZitadelSDK.UnitTests.Extensions;

public class ZitadelSdkBuilderExtensionsTests
{
    [Fact]
    public async Task WithPatAuth_FromConfiguration_RegistersCredentialProvider()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceAdmin:Authority"] = "https://example.com",
                ["ServiceAdmin:PersonalAccessToken"] = "pat-token",
                ["ServiceAdmin:AuthenticationType"] = "Token"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        var builder = services.AddZitadelSdk(configuration);

        // Act
        builder.WithPatAuth(configuration);

        var serviceProvider = services.BuildServiceProvider();

        var provider = serviceProvider.GetRequiredService<IZitadelCredentialProvider>();
        Assert.IsType<PersonalAccessTokenCredentialProvider>(provider);

        var options = serviceProvider.GetRequiredService<IOptions<ZitadelClientOptions>>().Value;
        var credentials = provider.CreateCallCredentials(options.Authority);
        var metadata = await CallCredentialsTestHelper.InvokeAsync(credentials);

        var header = Assert.Single(metadata);
        Assert.Equal("authorization", header.Key);
        Assert.Equal("Token pat-token", header.Value);
    }

    [Fact]
    public void WithPatAuth_MissingToken_Throws()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceAdmin:Authority"] = "https://example.com"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        var builder = services.AddZitadelSdk(configuration);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.WithPatAuth(configuration));
    }

    [Fact]
    public async Task WithJwtAuth_FromConfiguration_RegistersJwtProvider()
    {
        // Arrange
        var privateKey = CreatePrivateKey();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceAdmin:Authority"] = "https://example.com",
                ["ServiceAdmin:JwtProfile:KeyId"] = "key-id",
                ["ServiceAdmin:JwtProfile:ClientId"] = "client-id",
                ["ServiceAdmin:JwtProfile:Key"] = privateKey
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        var builder = services.AddZitadelSdk(configuration);

        // Act
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueResponse(CreateTokenResponse("token-1", 3600));

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));
        services.AddSingleton(httpClientFactory);

        builder.WithJwtAuth(configuration);

        var serviceProvider = services.BuildServiceProvider();

        var provider = serviceProvider.GetRequiredService<IZitadelCredentialProvider>();
        Assert.IsType<JwtProfileCredentialProvider>(provider);

        var credentials = provider.CreateCallCredentials("https://example.com");
        var metadata = await CallCredentialsTestHelper.InvokeAsync(credentials);

        var header = Assert.Single(metadata);
        Assert.Equal("authorization", header.Key);
        Assert.StartsWith("Bearer ", header.Value);
    }

    [Fact]
    public void WithJwtAuth_MissingSection_Throws()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceAdmin:Authority"] = "https://example.com"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();

        var builder = services.AddZitadelSdk(configuration);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => builder.WithJwtAuth(configuration));
        Assert.Contains("JWT Profile configuration section", exception.Message);
    }

    private static string CreatePrivateKey()
    {
        using var rsa = RSA.Create(2048);
        var privateKey = rsa.ExportPkcs8PrivateKey();
        return $"-----BEGIN PRIVATE KEY-----\n{Convert.ToBase64String(privateKey, Base64FormattingOptions.InsertLineBreaks)}\n-----END PRIVATE KEY-----";
    }

    private static HttpResponseMessage CreateTokenResponse(string token, int expiresIn)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"{{\"access_token\":\"{token}\",\"token_type\":\"Bearer\",\"expires_in\":{expiresIn}}}",
                Encoding.UTF8,
                "application/json")
        };
    }
}
