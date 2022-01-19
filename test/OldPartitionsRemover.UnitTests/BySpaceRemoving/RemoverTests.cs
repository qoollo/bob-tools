using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture.NUnit3;
using BobApi;
using BobApi.BobEntities;
using BobApi.Entities;
using BobToolsCli;
using BobToolsCli.BobApliClientFactories;
using BobToolsCli.ConfigurationFinding;
using FakeItEasy;
using FluentAssertions;
using NUnit.Framework;
using OldPartitionsRemover.BySpaceRemoving;
using OldPartitionsRemover.UnitTests.Attributes;

namespace OldPartitionsRemover.UnitTests.BySpaceRemoving;

public class RemoverTests
{
    [Test, SutFactory]
    public async Task RemoveOldPartitions_WithoutConfig_ReturnsError(
        [Frozen] IConfigurationFinder configurationFinder,
        Remover sut)
    {
        A.CallTo(() => configurationFinder.FindClusterConfiguration(A<CancellationToken>.Ignored))
            .Returns(YamlReadingResult<ClusterConfiguration>.Error(""));

        var result = await sut.RemovePartitionsBySpace(CancellationToken.None);

        result.IsOk(out var _, out var _).Should().BeFalse();
    }

    [Test, SutFactory]
    public async Task RemovePartitionsBySpace_WithEnoughSpace_ReturnsOk(
        [Frozen] Arguments arguments,
        [Frozen] ISpaceBobApiClient spaceBobApiClient,
        Remover sut)
    {
        arguments.ThresholdString = "1000B";
        A.CallTo(() => spaceBobApiClient.GetFreeSpaceBytes(A<CancellationToken>.Ignored))
            .Returns(BobApiResult<ulong>.Ok(2000));

        var result = await sut.RemovePartitionsBySpace(CancellationToken.None);

        result.IsOk(out var r, out var _).Should().BeTrue();
        r.Should().BeTrue();
    }

    [Test, SutFactory]
    public async Task RemovePartitionsBySpace_WithConnectionError_ReturnsError(
        [Frozen] ISpaceBobApiClient spaceBobApiClient,
        [Frozen] Arguments arguments,
        Remover sut)
    {
        arguments.ThresholdString = "1000B";
        A.CallTo(() => spaceBobApiClient.GetFreeSpaceBytes(A<CancellationToken>.Ignored))
            .Returns(BobApiResult<ulong>.Unavailable());

        var result = await sut.RemovePartitionsBySpace(CancellationToken.None);

        result.IsOk(out var _, out var e).Should().BeFalse();
        e.Should().ContainEquivalentOf("Unavailable");
    }

    [Test, SutFactory]
    public async Task RemovePartitionsBySpace_WithConnectionErrorAndErrorSkip_ReturnsOk(
        [Frozen] CommonArguments com,
        [Frozen] Arguments arguments,
        [Frozen] ISpaceBobApiClient spaceBobApiClient,
        Remover sut)
    {
        arguments.ThresholdString = "1000B";
        com.ContinueOnError = true;
        A.CallTo(() => spaceBobApiClient.GetFreeSpaceBytes(A<CancellationToken>.Ignored))
            .Returns(BobApiResult<ulong>.Unavailable());

        var result = await sut.RemovePartitionsBySpace(CancellationToken.None);

        result.IsOk(out var _, out var e).Should().BeTrue();
    }

    [Test, SutFactory]
    public async Task RemovePartitionsBySpace_WithNotEnoughSpace_RemovesSinglePartition(
        [Frozen] Arguments arguments,
        [Frozen] ISpaceBobApiClient spaceBobApiClient,
        [Frozen] IPartitionsBobApiClient partitionsBobApiClient,
        Remover sut)
    {
        arguments.ThresholdString = "1000B";
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

    [Test, SutFactory]
    public async Task RemovePartitionsBySpace_WithMultiplePartitions_RemovesUntilSpaceIsEnough(
       [Frozen] Arguments arguments,
       [Frozen] ISpaceBobApiClient spaceBobApiClient,
       [Frozen] IPartitionsBobApiClient partitionsBobApiClient,
       Remover sut)
    {
        arguments.ThresholdString = "1000B";
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

    [Test, SutFactory]
    public async Task RemovePartitionsBySpace_WithErrorInPartitionsCall_ReturnsError(
      [Frozen] Arguments arguments,
      [Frozen] ISpaceBobApiClient spaceBobApiClient,
      [Frozen] IPartitionsBobApiClient partitionsBobApiClient,
      Remover sut)
    {
        arguments.ThresholdString = "1000B";
        A.CallTo(() => spaceBobApiClient.GetFreeSpaceBytes(A<CancellationToken>.Ignored))
            .Returns(BobApiResult<ulong>.Ok(500));
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<List<string>>.Unavailable());

        var result = await sut.RemovePartitionsBySpace(CancellationToken.None);

        result.IsOk(out var _, out var err).Should().BeFalse();
        err.Should().ContainEquivalentOf("Unavailable");
    }

    [Test, SutFactory]
    public async Task RemovePartitionsBySpace_WithErrorInSinglePartitionCall_ReturnsError(
        [Frozen] Arguments arguments,
        [Frozen] ISpaceBobApiClient spaceBobApiClient,
        [Frozen] IPartitionsBobApiClient partitionsBobApiClient,
        Remover sut)
    {
        arguments.ThresholdString = "1000B";
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

    [Test, SutFactory]
    public async Task RemovePartitionsBySpace_WithErrorInDeleteCall_ReturnsError(
        [Frozen] Arguments arguments,
        [Frozen] ISpaceBobApiClient spaceBobApiClient,
        [Frozen] IPartitionsBobApiClient partitionsBobApiClient,
        Remover sut)
    {
        arguments.ThresholdString = "1000B";
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

    [Test, SutFactory]
    public async Task RemovePartitionsBySpace_WithErrorInFirstPartitionCallAndSkipErrors_ReturnsOk(
        [Frozen] CommonArguments com,
        [Frozen] Arguments arguments,
        [Frozen] ISpaceBobApiClient spaceBobApiClient,
        [Frozen] IPartitionsBobApiClient partitionsBobApiClient,
        Remover sut)
    {
        com.ContinueOnError = true;
        arguments.ThresholdString = "1000B";
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

    [Test, SutFactory]
    public async Task RemovePartitionsBySpace_WithSpaceFreedByFirstVdisk_DoesNotCheckSecondVdisk(
        [Frozen] IConfigurationFinder configurationFinder,
        [Frozen] Arguments arguments,
        [Frozen] ISpaceBobApiClient spaceBobApiClient,
        [Frozen] IPartitionsBobApiClient partitionsBobApiClient,
        Remover sut)
    {
        arguments.ThresholdString = "1000B";
        A.CallTo(() => configurationFinder.FindClusterConfiguration(A<CancellationToken>.Ignored))
            .Returns(YamlReadingResult<ClusterConfiguration>.Ok(new ClusterConfiguration
            {
                Nodes = new List<ClusterConfiguration.Node>
                {
                    new ClusterConfiguration.Node
                    {
                        Name = "node1"
                    }
                },
                VDisks = new List<ClusterConfiguration.VDisk>
                {
                    new ClusterConfiguration.VDisk
                    {
                        Id = 0,
                        Replicas = new List<ClusterConfiguration.VDisk.Replica>
                        {
                            new ClusterConfiguration.VDisk.Replica
                            {
                                Node = "node1"
                            }
                        }
                    },
                    new ClusterConfiguration.VDisk
                    {
                        Id = 1,
                        Replicas = new List<ClusterConfiguration.VDisk.Replica>
                        {
                            new ClusterConfiguration.VDisk.Replica
                            {
                                Node = "node1"
                            }
                        }
                    }
                }
            }));
        A.CallTo(() => spaceBobApiClient.GetFreeSpaceBytes(A<CancellationToken>.Ignored))
            .ReturnsNextFromSequence(BobApiResult<ulong>.Ok(500), BobApiResult<ulong>.Ok(1500));
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<List<string>>.Ok(new List<string> { "1", "2" }));
        A.CallTo(() => partitionsBobApiClient.GetPartition(0, "1", A<CancellationToken>.Ignored))
            .Returns(BobApiResult<Partition>.Ok(new Partition { Timestamp = 100 }));
        A.CallTo(() => partitionsBobApiClient.DeletePartitionsByTimestamp(0, 100, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<bool>.Ok(true));

        var result = await sut.RemovePartitionsBySpace(CancellationToken.None);

        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.That.Matches(v => v.Id == 1), A<CancellationToken>.Ignored))
            .MustNotHaveHappened();
    }
}