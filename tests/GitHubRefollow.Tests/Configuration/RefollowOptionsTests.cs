using GitHubRefollow.Configuration;

namespace GitHubRefollow.Tests.Configuration;

public sealed class RefollowOptionsTests
{
    [Fact]
    public void Defaults_KeepMutationsDisabledAndUsePersistentContainerPath()
    {
        var options = new RefollowOptions();

        Assert.True(options.DryRun);
        Assert.Equal(2, options.DelaySeconds);
        Assert.Equal("/data", options.DataPath);
        Assert.Equal("Asia/Ho_Chi_Minh", options.TimeZoneId);
    }
}
