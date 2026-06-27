using System.Net;
using LogCollectorApp.Helpers;
using NUnit.Framework;

namespace LogCollectorApp.Tests.Helpers;

[TestFixture]
public class IpAddressDbConverterTests
{
    [TestCase("10.10.130.6", "10.10.130.6")]
    [TestCase("127.0.0.1", "127.0.0.1")]
    [TestCase("255.255.255.255", "255.255.255.255")]
    public void ToDatabase_WhenIpAddressIsValid_ReturnsIpAddress(
        string value,
        string expected)
    {
        IPAddress result = IpAddressDbConverter.ToDatabase(value);

        Assert.That(result.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void ToDatabase_WhenValueContainsSpaces_TrimsValue()
    {
        IPAddress result = IpAddressDbConverter.ToDatabase(" 10.10.130.6 ");

        Assert.That(result.ToString(), Is.EqualTo("10.10.130.6"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("10.10.130.999")]
    [TestCase("abc")]
    [TestCase("10.10.130.abc")]
    public void ToDatabase_WhenIpAddressIsInvalid_ThrowsFormatException(string? value)
    {
        Assert.Throws<FormatException>(() =>
        {
            IpAddressDbConverter.ToDatabase(value!);
        });
    }

    [Test]
    public void FromDatabase_WhenIpAddressIsProvided_ReturnsStringValue()
    {
        IPAddress ipAddress = IPAddress.Parse("10.10.130.6");

        string result = IpAddressDbConverter.FromDatabase(ipAddress);

        Assert.That(result, Is.EqualTo("10.10.130.6"));
    }

    [TestCase("10.10.130.6")]
    [TestCase("127.0.0.1")]
    [TestCase(" 10.10.130.6 ")]
    public void IsValid_WhenIpAddressIsValid_ReturnsTrue(string value)
    {
        bool result = IpAddressDbConverter.IsValid(value);

        Assert.That(result, Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("10.10.130.999")]
    [TestCase("abc")]
    [TestCase("10.10.130.abc")]
    public void IsValid_WhenIpAddressIsInvalid_ReturnsFalse(string? value)
    {
        bool result = IpAddressDbConverter.IsValid(value!);

        Assert.That(result, Is.False);
    }
}