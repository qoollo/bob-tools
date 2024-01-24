using System.Threading;
using System.Threading.Tasks;
using BobApi;
using BobApi.Entities;
using FakeItEasy;
using FakeItEasy.Configuration;
using FluentAssertions;
using OldPartitionsRemover.Entities;

namespace OldPartitionsRemover.UnitTests;

public abstract class ResultAssertionsChecker
{
    protected virtual Result<int> GetResult() => Result<int>.Ok(0);

    protected virtual IPartitionsBobApiClient GetPartitionsBobApiClientMock() =>
        A.Fake<IPartitionsBobApiClient>();

    protected void AssertRunFailed(string? errorContent = null)
    {
        GetResult().IsOk(out var _, out var e).Should().BeFalse();
        if (errorContent != null)
            e.Should().ContainEquivalentOf(errorContent);
    }

    protected void AssertRunSucceeded()
    {
        GetResult()
            .IsOk(out var r, out var e)
            .Should()
            .BeTrue(because: $"error \"{e}\" should not occur");
    }

    protected void AssertRemovedCount(int removedCount)
    {
        AssertRunSucceeded();
        GetResult().IsOk(out var r, out var _);
        r.Should().Be(removedCount);
    }

    protected void AssertDeleteCalledExactTimes(int times) =>
        DeleteCall().MustHaveHappened(times, Times.Exactly);

    protected void AssertDeleteHappened(string? partitionId = null) =>
        DeleteCall(partitionId: partitionId).MustHaveHappened();

    protected void AssertDeleteNeverHappened(string? partitionId = null) =>
        DeleteCall(partitionId: partitionId).MustNotHaveHappened();

    protected void AssertAlienDeleteHappened(string? partitionId = null) =>
        DeleteAlienCall(partitionId: partitionId).MustHaveHappened();

    protected void AssertAlienDeleteNeverHappened(string? partitionId = null) =>
        DeleteAlienCall(partitionId: partitionId).MustNotHaveHappened();

    private IReturnValueArgumentValidationConfiguration<Task<BobApiResult<bool>>> DeleteCall(
        string? partitionId = null
    )
    {
        if (partitionId != null)
            return A.CallTo(
                () =>
                    GetPartitionsBobApiClientMock()
                        .DeletePartitionById(
                            A<string>.Ignored,
                            A<long>.Ignored,
                            partitionId,
                            A<CancellationToken>.Ignored
                        )
            );
        else
            return A.CallTo(
                () =>
                    GetPartitionsBobApiClientMock()
                        .DeletePartitionById(
                            A<string>.Ignored,
                            A<long>.Ignored,
                            A<string>.Ignored,
                            A<CancellationToken>.Ignored
                        )
            );
    }

    private IReturnValueArgumentValidationConfiguration<Task<BobApiResult<bool>>> DeleteAlienCall(
        string? partitionId = null
    )
    {
        if (partitionId != null)
            return A.CallTo(
                () =>
                    GetPartitionsBobApiClientMock()
                        .DeleteAlienPartitionById(
                            A<string>.Ignored,
                            A<long>.Ignored,
                            partitionId,
                            A<CancellationToken>.Ignored
                        )
            );
        else
            return A.CallTo(
                () =>
                    GetPartitionsBobApiClientMock()
                        .DeleteAlienPartitionById(
                            A<string>.Ignored,
                            A<long>.Ignored,
                            A<string>.Ignored,
                            A<CancellationToken>.Ignored
                        )
            );
    }
}
