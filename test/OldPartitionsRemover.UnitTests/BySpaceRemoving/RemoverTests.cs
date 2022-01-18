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
}