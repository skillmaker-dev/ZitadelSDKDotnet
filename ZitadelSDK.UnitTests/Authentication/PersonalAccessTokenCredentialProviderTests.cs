using ZitadelSDK.Authentication;
using ZitadelSDK.UnitTests.TestHelpers;

namespace ZitadelSDK.UnitTests.Authentication;

public class PersonalAccessTokenCredentialProviderTests
{
    [Fact]
    public async Task CreateCallCredentials_AddsAuthorizationHeader()
    {
        // Arrange
        var provider = new PersonalAccessTokenCredentialProvider("token-value", "Token");

        // Act
        var credentials = provider.CreateCallCredentials("https://example.com");
        var metadata = await CallCredentialsTestHelper.InvokeAsync(credentials);

        // Assert
        var header = Assert.Single(metadata);
        Assert.Equal("authorization", header.Key);
        Assert.Equal("Token token-value", header.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidToken_Throws(string? token)
    {
        Assert.Throws<ArgumentException>(() => new PersonalAccessTokenCredentialProvider(token!));
    }
}
