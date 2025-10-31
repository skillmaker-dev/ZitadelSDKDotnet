using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using ZitadelSDK.Authentication;
using ZitadelSDK.UnitTests.TestHelpers;

namespace ZitadelSDK.UnitTests.Authentication;

public class JwtProfileCredentialProviderTests
{
    [Fact]
    public async Task CreateCallCredentials_UsesCachedTokenAndAddsAuthorizationHeader()
    {
        // Arrange
        var config = CreateConfig();
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueResponse(CreateTokenResponse("token-1", expiresIn: 3600));

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));

        var logger = Substitute.For<ILogger<JwtProfileCredentialProvider>>();
        var provider = new JwtProfileCredentialProvider(config, httpClientFactory, logger);

        var credentials = provider.CreateCallCredentials("https://example.com");

        // Act
        var firstMetadata = await CallCredentialsTestHelper.InvokeAsync(credentials);
        var firstHeader = Assert.Single(firstMetadata);

        // Assert
        Assert.Equal("authorization", firstHeader.Key);
        Assert.Equal("Bearer token-1", firstHeader.Value);
        Assert.Equal(1, handler.CallCount);

        var secondMetadata = await CallCredentialsTestHelper.InvokeAsync(credentials);
        var secondHeader = Assert.Single(secondMetadata);
        Assert.Equal("Bearer token-1", secondHeader.Value);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task CreateCallCredentials_InvalidAuthority_Throws()
    {
        // Arrange
        var config = CreateConfig();
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var logger = Substitute.For<ILogger<JwtProfileCredentialProvider>>();
        var provider = new JwtProfileCredentialProvider(config, httpClientFactory, logger);

        var credentials = provider.CreateCallCredentials("invalid-authority");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => CallCredentialsTestHelper.InvokeAsync(credentials));
    }

    [Fact]
    public async Task CreateCallCredentials_NonSuccessResponseThrows()
    {
        // Arrange
        var config = CreateConfig();
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("error")
        });

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));

        var logger = Substitute.For<ILogger<JwtProfileCredentialProvider>>();
        var provider = new JwtProfileCredentialProvider(config, httpClientFactory, logger);

        var credentials = provider.CreateCallCredentials("https://example.com");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => CallCredentialsTestHelper.InvokeAsync(credentials));
        Assert.Contains("Failed to obtain access token", exception.Message);
    }

    private static JwtProfileConfig CreateConfig()
    {
        return new JwtProfileConfig
        {
            KeyId = "key-id",
            ClientId = "client-id",
            Key = CreatePrivateKey()
        };
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
