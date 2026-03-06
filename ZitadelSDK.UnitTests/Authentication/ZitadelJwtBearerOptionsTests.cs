using ZitadelSDK.Authentication;

namespace ZitadelSDK.UnitTests.Authentication;

public class ZitadelJwtBearerOptionsTests
{
    [Fact]
    public void SaveToken_DefaultsToFalse()
    {
        var options = new ZitadelJwtBearerOptions();

        Assert.False(options.SaveToken);
    }

    [Fact]
    public void ClockSkew_DefaultsToTwoMinutes()
    {
        var options = new ZitadelJwtBearerOptions();

        Assert.Equal(TimeSpan.FromMinutes(2), options.ClockSkew);
    }
}
