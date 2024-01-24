using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BobApi.Entities;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using OldPartitionsRemover.BySpaceRemoving;
using Xunit;

namespace OldPartitionsRemover.UnitTests.BySpaceRemoving;

public class RemoverTests : GenericRemoverTests
{
    private readonly Arguments _arguments = new();

    public RemoverTests()
    {
        _arguments.DelayMilliseconds = 0; // TODO Test this
    }

    [Fact]
    public async Task RemoveOldPartitions_WithoutConfig_ReturnsError()
    {
        ConfigurationReadingReturnsError("error");

        await Run();

        AssertRunFailed();
    }

    [Fact]
    public async Task RemovePartitionsBySpace_WithEnoughSpace_ReturnsZeroRemoved()
    {
        EnoughFreeSpace();

        await Run();

        AssertRemovedCount(0);
    }

    [Fact]
    public async Task RemovePartitionsBySpace_WithConnectionError_ReturnsError()
    {
        NotEnoughFreeSpace();
        PartitionSlimsReturns(BobApiResult<List<PartitionSlim>>.Unavailable());

        await Run();

        AssertRunFailed("Unavailable");
    }

    [Fact]
    public async Task RemovePartitionsBySpace_WithConnectionErrorAndErrorSkip_ReturnsOk()
    {
        NotEnoughFreeSpace();
        ContinueOnErrorIs(true);
        FreeSpaceReturns(BobApiResult<ulong>.Unavailable());
        NumberOfReturnedPartitionsIs(1);

        await Run();

        AssertRunSucceeded();
    }

    [Fact]
    public async Task RemovePartitionsBySpace_WithNotEnoughSpace_RemovesSinglePartition()
    {
        NotEnoughFreeSpace();
        NumberOfReturnedPartitionsIs(1);

        await Run();

        AssertDeleteCalledExactTimes(1);
    }

    [Fact]
    public async Task RemovePartitionsBySpace_WithNotEnoughSpace_ReturnsOne()
    {
        NotEnoughFreeSpace();
        NumberOfReturnedPartitionsIs(1);

        await Run();

        AssertRemovedCount(1);
    }

    [Fact]
    public async Task RemovePartitionsBySpace_WithMultiplePartitions_RemovesUntilSpaceIsEnough()
    {
        FreeSpaceIsEnoughtAfterDeletions(1);
        NumberOfReturnedPartitionsIs(2);

        await Run();

        AssertDeleteCalledExactTimes(1);
    }

    [Fact]
    public async Task RemovePartitionsBySpace_WithMultiplePartitions_ReturnsRemovedCount()
    {
        FreeSpaceIsEnoughtAfterDeletions(2);
        NumberOfReturnedPartitionsIs(3);

        await Run();

        AssertRemovedCount(2);
    }

    [Fact]
    public async Task RemovePartitionsBySpace_WithErrorInDeleteCall_ReturnsError()
    {
        NotEnoughFreeSpace();
        NumberOfReturnedPartitionsIs(1);
        DeletePartitionReturns(BobApiResult<bool>.Unavailable());

        await Run();

        AssertRunFailed("Unavailable");
    }

    [Fact]
    public async Task RemovePartitionsBySpace_WithErrorInFirstDeletePartitionCallAndSkipErrors_ReturnsOk()
    {
        ContinueOnErrorIs(true);
        NotEnoughFreeSpace();
        NumberOfReturnedPartitionsIs(2);
        DeletePartitionReturns(BobApiResult<bool>.Unavailable());
        DeletePartitionReturns(true);

        await Run();

        AssertRunSucceeded();
    }

    [Fact]
    public async Task RemovePartitionsBySpace_WithOccupiedSpaceFlag_RemovesUntilSpaceIsSmallerThanThreshold()
    {
        OccupiedSpaceIsLowAfterDeletions(1);
        PartitionSlimsReturns(new PartitionSlim(), new PartitionSlim());

        await Run();

        AssertDeleteCalledExactTimes(1);
    }

    [Fact]
    public async Task RemovePartitionsBySpace_WithAlienEnabled_RemovesAlien()
    {
        AllowAlienIs(true);
        ConfigurationReadingReturnsTwoNodes();
        NotEnoughFreeSpace();
        NumberOfReturnedAlienPartitionsIs(1);

        await Run();

        AssertAlienDeleteHappened();
    }

    private async Task Run()
    {
        var remover = new Remover(
            _arguments,
            _configurationFinder,
            _bobApiClientFactory,
            _resultsCombiner,
            _removablePartitionsFinder,
            _loggerFactory.CreateLogger<Remover>()
        );

        _result = await remover.RemovePartitionsBySpace(default);
    }

    private void EnoughFreeSpace()
    {
        _arguments.ThresholdString = "1000B";
        _arguments.ThresholdTypeString = "free";
        FreeSpaceReturns(2000);
    }

    private void NotEnoughFreeSpace()
    {
        _arguments.ThresholdString = "1000B";
        _arguments.ThresholdTypeString = "free";
        FreeSpaceReturns(500);
    }

    private void FreeSpaceIsEnoughtAfterDeletions(int deletionsRequired)
    {
        _arguments.ThresholdString = "1000B";
        _arguments.ThresholdTypeString = "free";
        var responses = Enumerable
            .Range(0, deletionsRequired)
            .Select(_ => 500ul)
            .Concat(new[] { 2000ul });
        FreeSpaceReturns(responses.ToArray());
    }

    private void OccupiedSpaceIsLowAfterDeletions(int deletionsRequired)
    {
        _arguments.ThresholdString = "1000B";
        _arguments.ThresholdTypeString = "occupied";
        var responses = Enumerable
            .Range(0, deletionsRequired)
            .Select(_ => 2000ul)
            .Concat(new[] { 500ul });
        OccupiedSpaceReturns(responses.ToArray());
    }

    protected override RemoverArguments GetArguments()
    {
        return _arguments;
    }
}
