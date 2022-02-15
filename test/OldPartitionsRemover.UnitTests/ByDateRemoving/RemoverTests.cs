using System;
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
using BobToolsCli.ConfigurationReading;
using FakeItEasy;
using FluentAssertions;
using NUnit.Framework;
using OldPartitionsRemover.ByDateRemoving;
using OldPartitionsRemover.UnitTests.Attributes;

namespace OldPartitionsRemover.UnitTests.ByDateRemoving;

public class RemoverTests
{
    [Test, SutFactory]
    public async Task RemoveOldPartitions_WithoutConfig_ReturnsError(
        [Frozen] IConfigurationFinder configurationFinder,
        Remover sut)
    {
        A.CallTo(() => configurationFinder.FindClusterConfiguration(A<CancellationToken>.Ignored))
            .Returns(ConfigurationReadingResult<ClusterConfiguration>.Error(""));

        var result = await sut.RemoveOldPartitions(CancellationToken.None);

        result.IsOk(out var _, out var _).Should().BeFalse();
    }

    [Test, SutFactory]
    public async Task RemoveOldPartitions_WithoutConnection_ReturnsError(
        [Frozen] IPartitionsBobApiClient partitionsBobApiClient,
        Remover sut)
    {
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<List<string>>.Unavailable());

        var result = await sut.RemoveOldPartitions(CancellationToken.None);

        result.IsOk(out var _, out var err).Should().BeFalse();
        err.Should().ContainEquivalentOf("Unavailable");
    }

    [Test, SutFactory]
    public async Task RemoveOldPartitions_WithoutConnectionWithContinueOnErrorFlag_ReturnsOk(
        [Frozen] IPartitionsBobApiClient partitionsBobApiClient,
        [Frozen] CommonArguments commonArguments,
        Remover sut)
    {
        commonArguments.ContinueOnError = true;
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<List<string>>.Unavailable());

        var result = await sut.RemoveOldPartitions(CancellationToken.None);

        result.IsOk(out var _, out var err).Should().BeTrue();
    }

    [Test, SutFactory]
    public async Task RemoveOldPartitions_WithFailOnPartitionFetch_ReturnsError(
        [Frozen] IPartitionsBobApiClient partitionsBobApiClient,
        Remover sut)
    {
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<List<string>>.Ok(new List<string> { "1", "2" }));
        A.CallTo(() => partitionsBobApiClient.GetPartition(A<long>.Ignored, A<string>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<Partition>.Unavailable());

        var result = await sut.RemoveOldPartitions(CancellationToken.None);

        result.IsOk(out var _, out var err).Should().BeFalse();
        err.Should().ContainEquivalentOf("unavailable");
    }

    [Test, SutFactory]
    public async Task RemoveOldPartitions_WithFailOnSecondPartitionFetch_ReturnsError(
        [Frozen] IPartitionsBobApiClient partitionsBobApiClient,
        Remover sut)
    {
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<List<string>>.Ok(new List<string> { "1", "2" }));
        A.CallTo(() => partitionsBobApiClient.GetPartition(A<long>.Ignored, A<string>.Ignored, A<CancellationToken>.Ignored))
            .ReturnsNextFromSequence(BobApiResult<Partition>.Ok(new Partition()), BobApiResult<Partition>.Unavailable());

        var result = await sut.RemoveOldPartitions(CancellationToken.None);

        result.IsOk(out var _, out var err).Should().BeFalse();
        err.Should().ContainEquivalentOf("Unavailable");
    }

    [Test, SutFactory]
    public async Task RemoveOldPartitions_WithPartitionsWithTimestampOverThreshold_DoesNotRemoveAnything(
        [Frozen] IPartitionsBobApiClient partitionsBobApiClient,
        [Frozen] Arguments arguments,
        Remover sut)
    {
        arguments.ThresholdString = "-1d";
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
             .Returns(BobApiResult<List<string>>.Ok(new List<string> { "1" }));
        A.CallTo(() => partitionsBobApiClient.GetPartition(A<long>.Ignored, A<string>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<Partition>.Ok(new Partition() { Timestamp = DateTimeOffset.MaxValue.ToUnixTimeSeconds() }));

        var result = await sut.RemoveOldPartitions(CancellationToken.None);

        A.CallTo(() => partitionsBobApiClient.DeletePartitionsByTimestamp(A<long>.Ignored, A<long>.Ignored, A<CancellationToken>.Ignored))
            .MustNotHaveHappened();
    }

    [Test, SutFactory]
    public async Task RemoveOldPartitions_WithPartitionsWithOldTimestampAndNewTimestamp_RemovesOldTimestampPartition(
        [Frozen] IPartitionsBobApiClient partitionsBobApiClient,
        [Frozen] Arguments arguments,
        Remover sut)
    {
        arguments.ThresholdString = "-1d";
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
             .Returns(BobApiResult<List<string>>.Ok(new List<string> { "1", "2" }));
        var oldTimestamp = DateTimeOffset.MinValue.ToUnixTimeSeconds();
        A.CallTo(() => partitionsBobApiClient.GetPartition(A<long>.Ignored, A<string>.Ignored, A<CancellationToken>.Ignored))
            .ReturnsNextFromSequence(
                BobApiResult<Partition>.Ok(new Partition() { Timestamp = DateTimeOffset.MaxValue.ToUnixTimeSeconds() }),
                BobApiResult<Partition>.Ok(new Partition() { Timestamp = oldTimestamp }));

        var result = await sut.RemoveOldPartitions(CancellationToken.None);

        A.CallTo(() => partitionsBobApiClient.DeletePartitionsByTimestamp(A<long>.Ignored, A<long>.That.IsEqualTo(oldTimestamp), A<CancellationToken>.Ignored))
            .MustHaveHappened();
    }

    [Test, SutFactory]
    public async Task RemoveOldPartitions_WithSuccessfullDeletion_ReturnsOk(
        [Frozen] IPartitionsBobApiClient partitionsBobApiClient,
        [Frozen] Arguments arguments,
        Remover sut)
    {
        arguments.ThresholdString = "-1d";
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
             .Returns(BobApiResult<List<string>>.Ok(new List<string> { "1", "2" }));
        A.CallTo(() => partitionsBobApiClient.GetPartition(A<long>.Ignored, A<string>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<Partition>.Ok(new Partition() { Timestamp = DateTimeOffset.MinValue.ToUnixTimeSeconds() }));
        A.CallTo(() => partitionsBobApiClient.DeletePartitionsByTimestamp(A<long>.Ignored, A<long>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<bool>.Ok(true));

        var result = await sut.RemoveOldPartitions(CancellationToken.None);

        result.IsOk(out var _, out var _).Should().BeTrue();
    }
}