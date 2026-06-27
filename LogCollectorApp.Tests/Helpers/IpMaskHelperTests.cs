using LogCollectorApp.Helpers;
using NUnit.Framework;

namespace LogCollectorApp.Tests.Helpers;

[TestFixture]
public class IpMaskHelperTests
{
    [TestCase("10.10.130.6")]
    [TestCase("10.10.130.*")]
    [TestCase("192.168.*.*")]
    [TestCase("*.*.*.*")]
    [TestCase("0.0.0.0")]
    [TestCase("255.255.255.255")]
    public void IsValidIpMask_WhenMaskIsValid_ReturnsTrue(string mask)
    {
        bool result = IpMaskHelper.IsValidIpMask(mask);

        Assert.That(result, Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("10.10.130")]
    [TestCase("10.10.130.1.5")]
    [TestCase("10.10.130.256")]
    [TestCase("10.10.130.-1")]
    [TestCase("10.10.130.abc")]
    [TestCase("...")]
    public void IsValidIpMask_WhenMaskIsInvalid_ReturnsFalse(string? mask)
    {
        bool result = IpMaskHelper.IsValidIpMask(mask!);

        Assert.That(result, Is.False);
    }

    [TestCase("10.10.130.*", "10.10.130.%")]
    [TestCase("192.168.*.*", "192.168.%.%")]
    [TestCase("*.*.*.*", "%.%.%.%")]
    [TestCase("10.10.130.6", "10.10.130.6")]
    public void ConvertToSqlLikePattern_WhenMaskContainsStars_ReplacesStarsWithPercent(
        string mask,
        string expected)
    {
        string result = IpMaskHelper.ConvertToSqlLikePattern(mask);

        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void ConvertToSqlLikePattern_WhenMaskIsEmptyOrWhiteSpace_ReturnsPercent(string? mask)
    {
        string result = IpMaskHelper.ConvertToSqlLikePattern(mask!);

        Assert.That(result, Is.EqualTo("%"));
    }
}