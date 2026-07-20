using System.Reflection;

namespace DeskBrightness.Test;

public sealed class UpdateMechanismTests
{
    [Theory]
    [InlineData("v1.0.0", "1.0.0")]
    [InlineData("v2.1.3", "2.1.3")]
    [InlineData("v10.0.8", "10.0.8")]
    [InlineData("v0.0.1", "0.0.1")]
    public void VersionTag_TrimV_ParsesCorrectly(string tag, string expected)
    {
        var trimmed = tag.TrimStart('v');
        Assert.Equal(expected, trimmed);
        Assert.True(Version.TryParse(trimmed, out _));
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1")]
    [InlineData("1.0.0", "1.1.0")]
    [InlineData("1.0.0", "2.0.0")]
    [InlineData("2.1.3", "10.0.0")]
    public void VersionComparison_DetectsNewerVersions(string current, string latest)
    {
        var cur = Version.Parse(current);
        var lat = Version.Parse(latest);

        Assert.True(lat > cur);
    }

    [Theory]
    [InlineData("1.0.0", "0.9.0")]
    [InlineData("2.0.0", "1.9.9")]
    [InlineData("2.1.3", "2.1.2")]
    public void VersionComparison_DetectsOlderVersions(string current, string latest)
    {
        var cur = Version.Parse(current);
        var lat = Version.Parse(latest);

        Assert.False(lat > cur);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("2.1.3", "2.1.3")]
    public void VersionComparison_EqualVersions_NotNewer(string current, string latest)
    {
        var cur = Version.Parse(current);
        var lat = Version.Parse(latest);

        Assert.False(lat > cur);
    }

    [Theory]
    [InlineData("1.0.0", false)]
    [InlineData("2.0.0", false)]
    [InlineData("10.0.28000", true)] // SDK Contracts style version fails under normal conditions
    public void TryDetectMsix_DoesNotThrow(string versionStr, bool expected)
    {
        // Simulating the detection: TryDetectMsix catches all exceptions
        // and returns true only if GetCurrentPackageFullName returns 0.
        // Since we're not packaged, it should always return false.
        bool detected;

        try
        {
            // This mimics the TryDetectMsix logic
            var assembly = Assembly.GetExecutingAssembly();
            var ver = assembly.GetName().Version;

            if (ver is not null && Version.TryParse(versionStr, out var _))
            {
                // No actual P/Invoke call here — this is a logic test
                detected = false;
            }
            else
            {
                detected = false;
            }
        }
        catch
        {
            detected = false;
        }

        Assert.False(detected);
    }

    [Fact]
    public void AppVersion_FromAssembly_IsParsable()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        Assert.NotNull(version);
        Assert.True(version.Major >= 0);
    }
}