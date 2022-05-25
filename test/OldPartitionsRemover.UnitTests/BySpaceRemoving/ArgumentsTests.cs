using FluentAssertions;
using NUnit.Framework;
using OldPartitionsRemover.BySpaceRemoving;

namespace OldPartitionsRemover.UnitTests.BySpaceRemoving;

public class ArgumentsTests
{
    [Test]
    public void GetThreshold_WithEmptyThresholdString_ReturnsError()
    {
        var arguments = new Arguments();

        var result = arguments.GetThreshold();

        result.IsOk(out var _, out var _).Should().BeFalse();
    }

    [Test]
    public void GetThreshold_WithGarbageString_ReturnsError()
    {
        var arguments = new Arguments
        {
            ThresholdString = "garbage"
        };

        var result = arguments.GetThreshold();

        result.IsOk(out var _, out var _).Should().BeFalse();
    }

    [TestCase("1000B", 1000)]
    [TestCase("2kB", 2000)]
    [TestCase("2kb", 2000)]
    public void GetThreshold_WithNumberWithB_ReturnsBytesSize(string s, double bytes)
    {
        var arguments = new Arguments
        {
            ThresholdString = s
        };

        var result = arguments.GetThreshold();

        result.IsOk(out var d, out var _).Should().BeTrue();
        d.Bytes.Should().Be(bytes);
    }
}