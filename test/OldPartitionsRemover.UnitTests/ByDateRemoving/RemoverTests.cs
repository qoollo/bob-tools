using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BobApi.Entities;
using Microsoft.Extensions.Logging;
using OldPartitionsRemover.ByDateRemoving;
using Xunit;

namespace OldPartitionsRemover.UnitTests.ByDateRemoving;

public class RemoverTests : GenericRemoverTests
{
    private static readonly DateTimeOffset s_thresholdString =
        new(2000, 01, 01, 0, 0, 0, TimeSpan.Zero);
    private readonly Arguments _arguments = new();

    public RemoverTests()
    {
        _arguments.ThresholdString = s_thresholdString.ToString();
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
        PartitionSlimsReturnsResponse(BobApiResult<List<PartitionSlim>>.Unavailable());

        await Run();

        AssertRunFailed("Unavailable");
    }

    [Fact]
    public async Task RemoveOldPartitions_WithoutConnectionWithContinueOnErrorFlag_ReturnsOk()
    {
        ContinueOnErrorIs(true);
        PartitionSlimsReturnsResponse(BobApiResult<List<PartitionSlim>>.Unavailable());

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
    public async Task RemoveOldPartitions_WithSuccessfullDeletion_ReturnsNumberOfDeletedPartitions()
    {
        NumberOfReturnedPartitionsIs(2);
        EveryPartitionIsOutdated();

        await Run();

        AssertRemovedCount(2);
    }

    [Fact]
    public async Task RemoveOldPartitions_WithAlienEnabled_DeletesAlienPartitions()
    {
        AllowAlienIs(true);
        ConfigurationReadingReturnsTwoNodes();
        NumberOfReturnedAlienPartitionsIs(1);
        EveryPartitionIsOutdated();

        await Run();

        AssertAlienDeleteHappened();
    }

    [Fact]
    public async Task RemoveOldPartitions_WithAlienDisabled_DoesNotDeleteAlienPartitions()
    {
        AllowAlienIs(false);
        ConfigurationReadingReturnsTwoNodes();
        NumberOfReturnedAlienPartitionsIs(1);
        EveryPartitionIsOutdated();

        await Run();

        AssertAlienDeleteNeverHappened();
    }

    [Fact]
    public async Task RemoveOldPartitions_WithAlienEnabledAndAliensNotOld_DoesNotDeleteAliens()
    {
        AllowAlienIs(true);
        ConfigurationReadingReturnsTwoNodes();
        NumberOfReturnedAlienPartitionsIs(1);
        EveryPartitionIsActual();

        await Run();

        AssertAlienDeleteNeverHappened();
    }

    [Fact]
    public async Task RemoveOldPartitions_WithNormalAndAlienOutdatedPartitions_RemovesBoth()
    {
        AllowAlienIs(true);
        ConfigurationReadingReturnsTwoNodes();
        NumberOfReturnedAlienPartitionsIs(1);
        NumberOfReturnedPartitionsIs(1);
        EveryPartitionIsOutdated();

        await Run();

        AssertAlienDeleteHappened();
        AssertDeleteHappened();
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

    private void EveryPartitionIsOutdated()
    {
        var ts = s_thresholdString.ToUnixTimeSeconds() - 1;
        SetAllPartitionsTimestamp(ts);
    }

    private void EveryPartitionIsActual()
    {
        var ts = s_thresholdString.ToUnixTimeSeconds() + 1;
        SetAllPartitionsTimestamp(ts);
    }

    protected override RemoverArguments GetArguments()
    {
        return _arguments;
    }
}
