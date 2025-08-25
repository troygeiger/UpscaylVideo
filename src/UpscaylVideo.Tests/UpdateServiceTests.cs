using UpscaylVideo.Services;

namespace UpscaylVideo.Tests;

public class UpdateServiceTests
{
    [Fact]
    public void NormalizeVersion_StripsNonDigitPrefixes_AndPreservesPrerelease()
    {
        Assert.Equal("0.1.0", UpdateService.NormalizeVersion("v0.1.0"));
        Assert.Equal("0.1.0-alpha.0.74", UpdateService.NormalizeVersion("p0.1.0-alpha.0.74"));
        Assert.Equal("0.0.0", UpdateService.NormalizeVersion(null));
        Assert.Equal("0.0.0", UpdateService.NormalizeVersion("   \t   "));
    }

    [Fact]
    public void CompareVersions_StableVsStable_Works()
    {
        Assert.True(UpdateService.CompareVersions("0.1.0", "0.1.1") < 0);
        Assert.True(UpdateService.CompareVersions("0.1.1", "0.1.0") > 0);
        Assert.Equal(0, UpdateService.CompareVersions("1.2.3", "1.2.3"));
    }

    [Fact]
    public void CompareVersions_PrereleaseOrdering_SameBase()
    {
        // alpha.0.74 is newer than alpha.0.63
        Assert.True(UpdateService.CompareVersions("0.1.0-alpha.0.74", "0.1.0-alpha.0.63") > 0);
        // numeric token precedence vs alpha token
        Assert.True(UpdateService.CompareVersions("0.1.0-1", "0.1.0-alpha") < 0);
    }

    [Fact]
    public void CompareVersions_PrereleaseVsStable()
    {
        // prerelease < stable of same base
        Assert.True(UpdateService.CompareVersions("0.1.0-alpha.0.74", "0.1.0") < 0);
        // stable > prerelease ignoring tag prefixes
        Assert.True(UpdateService.CompareVersions("v0.1.0", "p0.1.0-alpha.0.74") > 0);
    }

    [Fact]
    public void CompareVersions_NullAndEmptyHandling()
    {
        Assert.True(UpdateService.CompareVersions(null, "0.1.0") < 0);
        Assert.True(UpdateService.CompareVersions("v0.0.0", null) > 0);
        Assert.Equal(0, UpdateService.CompareVersions(null, null));
    }
}

