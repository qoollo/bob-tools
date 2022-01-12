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
using OldPartitionsRemover.ByDateRemoving;
using OldPartitionsRemover.UnitTests.Attributes;

namespace OldPartitionsRemover.UnitTests.ByDateRemoving;

public class RemoverTests
{
    [Test, SutFactory]
    public async Task RemoveOldPartitions_WithoutConnection_ReturnsError(
        [Frozen] IPartitionsBobApiClient partitionsBobApiClient,
        [Frozen] Arguments arguments,
        Remover sut
    )
    {
        arguments.ContinueOnError = false;
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<List<string>>.Unavailable());

        var result = await sut.RemoveOldPartitions(CancellationToken.None);

        result.IsOk(out var _, out var err).Should().BeFalse();
        err.Should().Contain("Unavailable");
    }

    [Test, SutFactory]
    public async Task RemoveOldPartitions_WithFailOnPartitionFetch_ReturnsError(
    [Frozen] IPartitionsBobApiClient partitionsBobApiClient,
    [Frozen] Arguments arguments,
    Remover sut
)
    {
        arguments.ContinueOnError = false;
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<List<string>>.Ok(new List<string> { "1", "2" }));
        A.CallTo(() => partitionsBobApiClient.GetPartition(A<long>.Ignored, A<string>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<Partition>.Unavailable());

        var result = await sut.RemoveOldPartitions(CancellationToken.None);

        result.IsOk(out var _, out var err).Should().BeFalse();
        err.Should().Contain("Unavailable");
    }

    [Test, SutFactory]
    public async Task RemoveOldPartitions_WithFailOnSecondPartitionFetch_ReturnsError(
        [Frozen] IPartitionsBobApiClient partitionsBobApiClient,
        [Frozen] Arguments arguments,
        Remover sut
    )
    {
        arguments.ContinueOnError = false;
        A.CallTo(() => partitionsBobApiClient.GetPartitions(A<ClusterConfiguration.VDisk>.Ignored, A<CancellationToken>.Ignored))
            .Returns(BobApiResult<List<string>>.Ok(new List<string> { "1", "2" }));
        A.CallTo(() => partitionsBobApiClient.GetPartition(A<long>.Ignored, A<string>.Ignored, A<CancellationToken>.Ignored))
            .ReturnsNextFromSequence(BobApiResult<Partition>.Ok(new Partition()), BobApiResult<Partition>.Unavailable());

        var result = await sut.RemoveOldPartitions(CancellationToken.None);

        result.IsOk(out var _, out var err).Should().BeFalse();
        err.Should().Contain("Unavailable");
    }
}