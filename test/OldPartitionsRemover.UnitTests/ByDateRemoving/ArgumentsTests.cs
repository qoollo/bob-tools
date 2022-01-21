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

    [Test]
    public void GetThreshold_WithMinusDayString_ReturnsThresholdWithinPreviousTwoDays()
    {
        var arguments = new Arguments
        {
            ThresholdString = "-1d"
        };

        var threshold = arguments.GetThreshold();

        threshold.IsOk(out var d, out var _).Should().BeTrue();
        d.Should().BeBefore(DateTimeOffset.Now);
        d.Should().BeAfter(DateTimeOffset.Now - TimeSpan.FromDays(2));
    }

    [Test]
    public void GetThreshold_WithMinusHourString_ReturnsThresholdWithinPreviousTwoHours()
    {
        var arguments = new Arguments
        {
            ThresholdString = "-1h"
        };

        var threshold = arguments.GetThreshold();

        threshold.IsOk(out var d, out var _).Should().BeTrue();
        d.Should().BeBefore(DateTimeOffset.Now);
        d.Should().BeAfter(DateTimeOffset.Now - TimeSpan.FromHours(2));
    }

    [Test]
    public void GetThreshold_WithMinusMonthString_ReturnsThresholdWithin10Days()
    {
        var arguments = new Arguments
        {
            ThresholdString = "-1m"
        };

        var threshold = arguments.GetThreshold();

        threshold.IsOk(out var d, out var _).Should().BeTrue();
        d.Should().BeBefore(DateTimeOffset.Now - TimeSpan.FromDays(20));
        d.Should().BeAfter(DateTimeOffset.Now - TimeSpan.FromDays(40));
    }

    [Test]
    public void GetThreshold_WithMinusYearString_ReturnsThresholdWithinHundredDays()
    {
        var arguments = new Arguments
        {
            ThresholdString = "-1y"
        };

        var threshold = arguments.GetThreshold();

        threshold.IsOk(out var d, out var _).Should().BeTrue();
        d.Should().BeBefore(DateTimeOffset.Now - TimeSpan.FromDays(300));
        d.Should().BeAfter(DateTimeOffset.Now - TimeSpan.FromDays(400));
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