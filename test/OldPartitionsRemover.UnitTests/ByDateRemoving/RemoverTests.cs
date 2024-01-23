using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BobApi;
using BobApi.BobEntities;
using BobApi.Entities;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using OldPartitionsRemover.ByDateRemoving;
using Xunit;

namespace OldPartitionsRemover.UnitTests.ByDateRemoving;

public class RemoverTests : GenericRemoverTests
{
    private readonly Arguments _arguments = new();

    public RemoverTests()
    {
        _arguments.ThresholdString = "01.01.2000";
    }

    [Fact]
    public async Task RemoveOldPartitions_WithoutConfig_ReturnsError()
    {
        ConfigurationReadingReturnsError("");

        await Run();

        AssertRunFailed();
    }

    [Fact]
    public async Task RemoveOldPartitions_WithoutConnection_ReturnsError()
    {
        PartitionSlimsReturns(BobApiResult<List<PartitionSlim>>.Unavailable());

        await Run();

        AssertRunFailed("Unavailable");
    }

    [Fact]
    public async Task RemoveOldPartitions_WithoutConnectionWithContinueOnErrorFlag_ReturnsOk()
    {
        ContinueOnErrorIs(true);
        PartitionSlimsReturns(BobApiResult<List<PartitionSlim>>.Unavailable());

        await Run();

        AssertRunSucceeded();
    }

    [Fact]
    public async Task RemoveOldPartitions_WithPartitionsWithTimestampOverThreshold_DoesNotRemoveAnything()
    {
        PartitionSlimsReturns(
            new PartitionSlim { Timestamp = DateTimeOffset.MaxValue.ToUnixTimeSeconds() }
        );

        await Run();

        AssertDeleteNeverHappened();
    }

    [Fact]
    public async Task RemoveOldPartitions_WithPartitionsWithOldTimestampAndNewTimestamp_RemovesOldTimestampPartition()
    {
        PartitionSlimsReturns(
            new PartitionSlim { Timestamp = DateTimeOffset.MaxValue.ToUnixTimeSeconds() },
            new PartitionSlim
            {
                Id = "deleted",
                Timestamp = DateTimeOffset.MinValue.ToUnixTimeSeconds()
            }
        );

        await Run();

        AssertDeleteHappened("deleted");
    }

    [Fact]
    public async Task RemoveOldPartitions_WithPartitionsWithOldTimestampAndNewTimestamp_DoesNotRemoveNewTimestampPartition()
    {
        PartitionSlimsReturns(
            new PartitionSlim
            {
                Id = "not-deleted",
                Timestamp = DateTimeOffset.MaxValue.ToUnixTimeSeconds()
            },
            new PartitionSlim { Timestamp = DateTimeOffset.MinValue.ToUnixTimeSeconds() }
        );

        await Run();

        AssertDeleteNeverHappened("not-deleted");
    }

    [Fact]
    public async Task RemoveOldPartitions_WithSuccessfullDeletion_ReturnsOk()
    {
        NumberOfReturnedPartitionsIs(2);

        await Run();

        AssertRunSucceeded();
    }

    [Fact]
    public async Task RemoveOldPartitions_WithSuccessfullDeletion_ReturnsTrue()
    {
        NumberOfReturnedPartitionsIs(2);
        EveryPartitionIsOutdated();

        await Run();

        AssertRemovedCount(2);
    }

    private async Task Run()
    {
        var remover = new Remover(
            _arguments,
            _loggerFactory.CreateLogger<Remover>(),
            _bobApiClientFactory,
            _configurationFinder,
            _resultsCombiner,
            _removablePartitionsFinder
        );
        _result = await remover.RemoveOldPartitions(default);
    }

    private void EveryPartitionIsOutdated() =>
        _arguments.ThresholdString = DateTime.MaxValue.ToString();

    protected override RemoverArguments GetArguments()
    {
        return _arguments;
    }
}
