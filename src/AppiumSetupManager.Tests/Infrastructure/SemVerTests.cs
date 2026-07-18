using AppiumSetupManager.Core.Infrastructure;
using FluentAssertions;
using Xunit;

namespace AppiumSetupManager.Tests.Infrastructure;

public class SemVerTests
{
    [Fact]
    public void IsNewer_CandidateNewer_ReturnsTrue()
    {
        SemVer.IsNewer("2.1.0", "2.2.0").Should().BeTrue();
    }

    [Fact]
    public void IsNewer_CandidateOlder_ReturnsFalse()
    {
        SemVer.IsNewer("2.2.0", "2.1.0").Should().BeFalse();
    }

    [Fact]
    public void IsNewer_Equal_ReturnsFalse()
    {
        SemVer.IsNewer("2.2.0", "2.2.0").Should().BeFalse();
    }

    [Fact]
    public void IsNewer_LeadingVAndPreReleaseSuffix_AreNormalised()
    {
        SemVer.IsNewer("v18.2.0", "v18.3.0-beta.1").Should().BeTrue();
    }

    [Fact]
    public void IsNewer_UnparseableInstalled_ReturnsFalse()
    {
        SemVer.IsNewer("not-a-version", "2.2.0").Should().BeFalse();
    }

    [Fact]
    public void IsNewer_UnparseableCandidate_ReturnsFalse()
    {
        SemVer.IsNewer("2.1.0", "not-a-version").Should().BeFalse();
    }

    [Fact]
    public void IsNewer_NullInstalled_ReturnsFalse()
    {
        SemVer.IsNewer(null, "2.2.0").Should().BeFalse();
    }

    [Fact]
    public void IsNewer_NullCandidate_ReturnsFalse()
    {
        SemVer.IsNewer("2.1.0", null).Should().BeFalse();
    }

    [Fact]
    public void IsNewer_BothNull_ReturnsFalse()
    {
        SemVer.IsNewer(null, null).Should().BeFalse();
    }

    [Fact]
    public void IsNewer_EmptyStrings_ReturnFalse()
    {
        SemVer.IsNewer("", "").Should().BeFalse();
    }
}
