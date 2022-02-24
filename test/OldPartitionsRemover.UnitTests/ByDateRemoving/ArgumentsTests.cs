using System;
using FluentAssertions;
using NUnit.Framework;
using OldPartitionsRemover.ByDateRemoving;
using OldPartitionsRemover.Entities;

namespace OldPartitionsRemover.UnitTests.ByDateRemoving;

public class ArgumentsTests
{
    [Test]
    public void GetThreshold_WithEmptyThresholdString_ReturnsError()
    {
        var arguments = new Arguments();

        var threshold = arguments.GetThreshold();

        threshold.IsOk(out var _, out var _).Should().BeFalse();
    }

    [Test]
    public void GetThreshold_WithGarbageString_ReturnsError()
    {
        var arguments = new Arguments()
        {
            ThresholdString = "threshold"
        };

        var threshold = arguments.GetThreshold();

        threshold.IsOk(out var _, out var _).Should().BeFalse();
    }

    [TestCase("-1y", "400.00:00:00", "300.00:00:00")]
    [TestCase("-1m", "40.00:00:00", "20.00:00:00")]
    [TestCase("-1d", "1.01:00:00", "0.23:00:00")]
    [TestCase("-1h", "01:10:00", "00:50:00")]
    public void GetThreshold_WithThresholdAndEps_LiesWithinInterval(string thresholdString, string minBefore, string maxBefore)
    {
        var arguments = new Arguments { ThresholdString = thresholdString };

        var threshold = arguments.GetThreshold();

        var dt = DateTimeOffset.Now;
        threshold.IsOk(out var d, out var _).Should().BeTrue();
        d.Should().BeBefore(dt - TimeSpan.Parse(maxBefore));
        d.Should().BeAfter(dt - TimeSpan.Parse(minBefore));
    }

    [Test]
    public void GetThreshold_WithDateString_ReturnsExactThreshold()
    {
        var arguments = new Arguments
        {
            ThresholdString = "2020-12-01"
        };

        var threshold = arguments.GetThreshold();

        threshold.IsOk(out var d, out var _).Should().BeTrue();
        d.Should().Be(new DateTime(2020, 12, 01));
    }
}