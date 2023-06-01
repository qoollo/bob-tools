using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using AutoFixture.AutoFakeItEasy;
using AutoFixture.NUnit3;
using BobApi;
using BobApi.BobEntities;
using BobApi.Entities;
using BobToolsCli;
using BobToolsCli.BobApliClientFactories;
using BobToolsCli.ConfigurationFinding;
using BobToolsCli.ConfigurationReading;
using FakeItEasy;
using FluentAssertions;
using NUnit.Framework;
using OldPartitionsRemover.BySpaceRemoving;
using OldPartitionsRemover.UnitTests.Customizations;

namespace OldPartitionsRemover.UnitTests.BySpaceRemoving;

public class RemoverTests
{
    [Test, AD]
    public async Task RemoveOldPartitions_WithoutConfig_ReturnsError(
        IConfigurationFinder configurationFinder,
        Remover sut)
    {
        A.CallTo(() => configurationFinder.FindClusterConfiguration(A<bool>.Ignored, A<CancellationToken>.Ignored))
            .Returns(ConfigurationReadingResult<ClusterConfiguration>.Error(""));

        var result = await sut.RemovePartitionsBySpace(CancellationToken.None);

        result.IsOk(out var _, out var _).Should().BeFalse();
    }

    [Test, AD]
    public async Task RemovePartitionsBySpace_WithEnoughSpace_ReturnsZeroRemoved(
        ISpaceBobApiClient spaceBobApiClient,
        Remover sut)
    {
        A.CallTo(() => spaceBobApiClient.GetFreeSpaceBytes(A<CancellationToken>.Ignored))
            .Returns(BobApiResult<ulong>.Ok(2000));

        var result = await sut.RemovePartitionsBySpace(CancellationToken.None);

        result.IsOk(out var r, out var _).Should().BeTrue();

        r.Should().Be(0);
    }

    [Test, AD]
    public async Task RemovePartitionsBySpace_WithConnectionError_ReturnsError(
        IPartitionsBobApiClient partitionsBobApiClient,
        Remover sut)
    {
        A.CallTo(() => partitionsBobApiClient.GetPartitions(null, default))
            .WithAnyArguments().Returns(BobApiResult<List<string>>.Unavailable());

        var result = await sut.RemovePartitionsBySpace(CancellationToken.None);

        result.IsOk(out var _, out var e).Should().BeFalse();
        e.Should().ContainEquivalentOf("Unavailable");
    }

    [Test, AD]
    public async Task RemovePartitionsBySpace_WithConnectionErrorAndErrorSkip_ReturnsOk(
        Arguments arguments,
        ISpaceBobApiClient spaceBobApiClient,
        Remover sut)
    {
        arguments.ContinueOnError = true;
        A.CallTo(() => spaceBobApiClient.GetFreeSpaceBytes(A<CancellationToken>.Ignored))
            .Returns(BobApiResult<ulong>.Unavailable());

        var result = await sut.RemovePartitionsBySpace(CancellationToken.None);

        result.IsOk(out var _, out var e).Should().BeTrue();
    }

    [Test, AD]
    public async Task RemovePartitionsBySpace_WithNotEnoughSpace_RemovesSinglePartition(
        ISpaceBobApiClient spaceBobApiClient,
        IPartitionsBobApiClient partitionsBobApiClient,
        Remover sut)
    {
        A.CallTo(() => spaceBobApiClient.GetFreeSpaceBytes(A<CancellationToken>.Ignored))
            .Returns(BobApiResult<ulong>.Ok(500));
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<List<string>>.Ok(new List<string> { "1" }));
        A.CallTo(() => partitionsBobApiClient.GetPartition(0, "1", A<CancellationToken>.Ignored))
            .Returns(BobApiResult<Partition>.Ok(new Partition { Timestamp = 100 }));

        var result = await sut.RemovePartitionsBySpace(CancellationToken.None);

        A.CallTo(() => partitionsBobApiClient.DeletePartitionsByTimestamp(0, 100, A<CancellationToken>.Ignored))
            .MustHaveHappened();
    }

    [Test, AD]
    public async Task RemovePartitionsBySpace_WithNotEnoughSpace_ReturnsOne(
        ISpaceBobApiClient spaceBobApiClient,
        IPartitionsBobApiClient partitionsBobApiClient,
        Remover sut)
    {
        A.CallTo(() => spaceBobApiClient.GetFreeSpaceBytes(A<CancellationToken>.Ignored))
            .Returns(BobApiResult<ulong>.Ok(500));
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<List<string>>.Ok(new List<string> { "1" }));
        A.CallTo(() => partitionsBobApiClient.GetPartition(0, "1", A<CancellationToken>.Ignored))
            .Returns(BobApiResult<Partition>.Ok(new Partition { Timestamp = 100 }));
        A.CallTo(() => partitionsBobApiClient.DeletePartitionsByTimestamp(default, 0, default)).WithAnyArguments()
            .Returns(BobApiResult<bool>.Ok(true));

        var result = await sut.RemovePartitionsBySpace(CancellationToken.None);
        result.IsOk(out var count, out var _);

        count.Should().Be(1);
    }

    [Test, AD]
    public async Task RemovePartitionsBySpace_WithMultiplePartitions_RemovesUntilSpaceIsEnough(
       ISpaceBobApiClient spaceBobApiClient,
       IPartitionsBobApiClient partitionsBobApiClient,
       Remover sut)
    {
        A.CallTo(() => spaceBobApiClient.GetFreeSpaceBytes(A<CancellationToken>.Ignored))
            .ReturnsNextFromSequence(BobApiResult<ulong>.Ok(500), BobApiResult<ulong>.Ok(1500));
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<List<string>>.Ok(new List<string> { "1", "2" }));
        A.CallTo(() => partitionsBobApiClient.GetPartition(0, "1", A<CancellationToken>.Ignored))
            .Returns(BobApiResult<Partition>.Ok(new Partition { Timestamp = 200 }));
        A.CallTo(() => partitionsBobApiClient.GetPartition(0, "2", A<CancellationToken>.Ignored))
            .Returns(BobApiResult<Partition>.Ok(new Partition { Timestamp = 100 }));

        var result = await sut.RemovePartitionsBySpace(CancellationToken.None);

        A.CallTo(() => partitionsBobApiClient.DeletePartitionsByTimestamp(0, 200, A<CancellationToken>.Ignored))
            .MustNotHaveHappened();
    }

    [Test, AD]
    public async Task RemovePartitionsBySpace_WithMultiplePartitions_ReturnsRemovedCount(
       ISpaceBobApiClient spaceBobApiClient,
       IPartitionsBobApiClient partitionsBobApiClient,
       Remover sut)
    {
        A.CallTo(() => spaceBobApiClient.GetFreeSpaceBytes(A<CancellationToken>.Ignored))
            .ReturnsNextFromSequence(BobApiResult<ulong>.Ok(500), BobApiResult<ulong>.Ok(900), BobApiResult<ulong>.Ok(1500));
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<List<string>>.Ok(new List<string> { "1", "2", "3" }));
        A.CallTo(() => partitionsBobApiClient.GetPartition(0, "1", A<CancellationToken>.Ignored))
            .Returns(BobApiResult<Partition>.Ok(new Partition { Timestamp = 200 }));
        A.CallTo(() => partitionsBobApiClient.GetPartition(0, "2", A<CancellationToken>.Ignored))
            .Returns(BobApiResult<Partition>.Ok(new Partition { Timestamp = 150 }));
        A.CallTo(() => partitionsBobApiClient.GetPartition(0, "3", A<CancellationToken>.Ignored))
            .Returns(BobApiResult<Partition>.Ok(new Partition { Timestamp = 100 }));
        A.CallTo(() => partitionsBobApiClient.DeletePartitionsByTimestamp(default, 0, default)).WithAnyArguments()
            .Returns(BobApiResult<bool>.Ok(true));

        var result = await sut.RemovePartitionsBySpace(CancellationToken.None);
        result.IsOk(out var count, out var _);

        count.Should().Be(2);
    }

    [Test, AD]
    public async Task RemovePartitionsBySpace_WithErrorInPartitionsCall_ReturnsError(
        ISpaceBobApiClient spaceBobApiClient,
        IPartitionsBobApiClient partitionsBobApiClient,
        Remover sut)
    {
        A.CallTo(() => spaceBobApiClient.GetFreeSpaceBytes(A<CancellationToken>.Ignored))
            .Returns(BobApiResult<ulong>.Ok(500));
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<List<string>>.Unavailable());

        var result = await sut.RemovePartitionsBySpace(CancellationToken.None);

        result.IsOk(out var _, out var err).Should().BeFalse();
        err.Should().ContainEquivalentOf("Unavailable");
    }

    [Test, AD]
    public async Task RemovePartitionsBySpace_WithErrorInSinglePartitionCall_ReturnsError(
        ISpaceBobApiClient spaceBobApiClient,
        IPartitionsBobApiClient partitionsBobApiClient,
        Remover sut)
    {
        A.CallTo(() => spaceBobApiClient.GetFreeSpaceBytes(A<CancellationToken>.Ignored))
            .Returns(BobApiResult<ulong>.Ok(500));
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<List<string>>.Ok(new List<string> { "1", "2" }));
        A.CallTo(() => partitionsBobApiClient.GetPartition(0, "1", A<CancellationToken>.Ignored))
            .Returns(BobApiResult<Partition>.Unavailable());

        var result = await sut.RemovePartitionsBySpace(CancellationToken.None);

        result.IsOk(out var _, out var err).Should().BeFalse();
        err.Should().ContainEquivalentOf("Unavailable");
    }

    [Test, AD]
    public async Task RemovePartitionsBySpace_WithErrorInDeleteCall_ReturnsError(
        ISpaceBobApiClient spaceBobApiClient,
        IPartitionsBobApiClient partitionsBobApiClient,
        Remover sut)
    {
        A.CallTo(() => spaceBobApiClient.GetFreeSpaceBytes(A<CancellationToken>.Ignored))
            .Returns(BobApiResult<ulong>.Ok(500));
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<List<string>>.Ok(new List<string> { "1", "2" }));
        A.CallTo(() => partitionsBobApiClient.GetPartition(0, "1", A<CancellationToken>.Ignored))
            .Returns(BobApiResult<Partition>.Ok(new Partition { Timestamp = 200 }));
        A.CallTo(() => partitionsBobApiClient.DeletePartitionsByTimestamp(0, 200, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<bool>.Unavailable());

        var result = await sut.RemovePartitionsBySpace(CancellationToken.None);

        result.IsOk(out var _, out var err).Should().BeFalse();
        err.Should().ContainEquivalentOf("Unavailable");
    }

    [Test, AD]
    public async Task RemovePartitionsBySpace_WithErrorInFirstPartitionCallAndSkipErrors_ReturnsOk(
        Arguments arguments,
        ISpaceBobApiClient spaceBobApiClient,
        IPartitionsBobApiClient partitionsBobApiClient,
        Remover sut)
    {
        arguments.ContinueOnError = true;
        A.CallTo(() => spaceBobApiClient.GetFreeSpaceBytes(A<CancellationToken>.Ignored))
            .Returns(BobApiResult<ulong>.Ok(500));
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<List<string>>.Ok(new List<string> { "1", "2" }));
        A.CallTo(() => partitionsBobApiClient.GetPartition(0, "1", A<CancellationToken>.Ignored))
            .Returns(BobApiResult<Partition>.Unavailable());
        A.CallTo(() => partitionsBobApiClient.GetPartition(0, "2", A<CancellationToken>.Ignored))
            .Returns(BobApiResult<Partition>.Ok(new Partition { Timestamp = 300 }));
        A.CallTo(() => partitionsBobApiClient.DeletePartitionsByTimestamp(0, 200, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<bool>.Unavailable());

        var result = await sut.RemovePartitionsBySpace(CancellationToken.None);

        result.IsOk(out var _, out var err).Should().BeTrue();
    }

    [Test, AD]
    public async Task RemovePartitionsBySpace_WithOccupiedSpaceFlag_RemovesUntilSpaceIsSmallerThanThreshold(
        Arguments arguments,
        ISpaceBobApiClient spaceBobApiClient,
        IPartitionsBobApiClient partitionsBobApiClient,
        Remover sut)
    {
        arguments.ThresholdTypeString = "occupied";
        A.CallTo(() => spaceBobApiClient.GetOccupiedSpaceBytes(A<CancellationToken>.Ignored))
            .ReturnsNextFromSequence(BobApiResult<ulong>.Ok(1500), BobApiResult<ulong>.Ok(500));
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<List<string>>.Ok(new List<string> { "1", "2" }));
        A.CallTo(() => partitionsBobApiClient.GetPartition(0, "1", A<CancellationToken>.Ignored))
            .Returns(BobApiResult<Partition>.Ok(new Partition { Timestamp = 200 }));
        A.CallTo(() => partitionsBobApiClient.GetPartition(0, "2", A<CancellationToken>.Ignored))
            .Returns(BobApiResult<Partition>.Ok(new Partition { Timestamp = 100 }));

        var result = await sut.RemovePartitionsBySpace(CancellationToken.None);

        A.CallTo(() => partitionsBobApiClient.DeletePartitionsByTimestamp(0, 200, A<CancellationToken>.Ignored))
            .MustNotHaveHappened();
    }

    private class ADAttribute : AutoDataAttribute
    {
        public ADAttribute() : base(() =>
        {
            var fixture = new Fixture();
            fixture.Customize(new FrozenApiClientsCustomization());
            fixture.Customize(new SingleNodeConfigCustomization());
            fixture.Customize(new FrozenArgumentsCustomization<Arguments>(args =>
            {
                args.DelayMilliseconds = 0;
                args.ThresholdString = "1000B";
                args.ContinueOnError = false;
                args.ThresholdTypeString = "free";
            }));

            return fixture;
        })
        { }
    }
}
