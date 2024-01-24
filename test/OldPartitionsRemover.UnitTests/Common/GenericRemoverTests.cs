using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BobApi;
using BobApi.BobEntities;
using BobApi.Entities;
using BobToolsCli.BobApliClientFactories;
using BobToolsCli.ConfigurationFinding;
using BobToolsCli.ConfigurationReading;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OldPartitionsRemover.Entities;
using OldPartitionsRemover.Infrastructure;

namespace OldPartitionsRemover.UnitTests;

public abstract class GenericRemoverTests : ResultAssertionsChecker
{
    protected readonly IConfigurationFinder _configurationFinder = A.Fake<IConfigurationFinder>();
    protected readonly ISpaceBobApiClient _spaceBobApiClient = A.Fake<ISpaceBobApiClient>();
    protected readonly IPartitionsBobApiClient _partitionsBobApiClient =
        A.Fake<IPartitionsBobApiClient>();
    protected readonly IBobApiClientFactory _bobApiClientFactory = A.Fake<IBobApiClientFactory>();
    protected readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    protected readonly ResultsCombiner _resultsCombiner;
    protected readonly RemovablePartitionsFinder _removablePartitionsFinder;

    // Prepared state
    private readonly List<
        ConfigurationReadingResult<ClusterConfiguration>
    > _configurationReadingResults = new();
    private readonly List<BobApiResult<List<PartitionSlim>>> _partitionSlimsResults = new();
    private readonly List<BobApiResult<List<PartitionSlim>>> _alienPartitionSlimsResults = new();
    private readonly List<BobApiResult<bool>> _deletePartitionByIdResults = new();
    private readonly List<BobApiResult<ulong>> _freeSpaceResults = new();
    private readonly List<BobApiResult<ulong>> _occupiedSpaceResults = new();
    private long? _allPartitionsDefaultTimestamp = null;

    protected abstract RemoverArguments GetArguments();

    protected Result<int>? _result;

    public GenericRemoverTests()
    {
        A.CallTo<Task<BobApiResult<List<PartitionSlim>>>>(
                () => _partitionsBobApiClient.GetPartitionSlims(default, default, default)
            )
            .WithAnyArguments()
            .ReturnsLazily(
                CreateReturner(
                    _partitionSlimsResults,
                    new List<PartitionSlim>(),
                    ApplyDefaultValues
                )
            );
        A.CallTo(() => _partitionsBobApiClient.GetAlienPartitionSlims(default, default, default))
            .WithAnyArguments()
            .ReturnsLazily(
                CreateReturner(
                    _alienPartitionSlimsResults,
                    new List<PartitionSlim>(),
                    ApplyDefaultValues
                )
            );
        A.CallTo(_partitionsBobApiClient)
            .WithReturnType<Task<BobApiResult<bool>>>()
            .ReturnsLazily(CreateReturner(_deletePartitionByIdResults, true));
        A.CallTo(_configurationFinder)
            .WithReturnType<Task<ConfigurationReadingResult<ClusterConfiguration>>>()
            .ReturnsLazily(
                CreateReturner(
                    _configurationReadingResults,
                    TestConstants.DefaultClusterConfiguration
                )
            );
        A.CallTo(() => _spaceBobApiClient.GetFreeSpaceBytes(default))
            .WithAnyArguments()
            .ReturnsLazily(CreateReturner(_freeSpaceResults, 0ul));
        A.CallTo(() => _spaceBobApiClient.GetOccupiedSpaceBytes(default))
            .WithAnyArguments()
            .ReturnsLazily(CreateReturner(_occupiedSpaceResults, 0ul));

        A.CallTo(_bobApiClientFactory)
            .WithReturnType<ISpaceBobApiClient>()
            .Returns(_spaceBobApiClient);
        A.CallTo(_bobApiClientFactory)
            .WithReturnType<IPartitionsBobApiClient>()
            .Returns(_partitionsBobApiClient);

        _resultsCombiner = new(GetArguments(), _loggerFactory.CreateLogger<ResultsCombiner>());
        _removablePartitionsFinder = new(_resultsCombiner, _bobApiClientFactory, GetArguments());
    }

    protected void ContinueOnErrorIs(bool value) => GetArguments().ContinueOnError = value;

    protected void AllowAlienIs(bool value) => GetArguments().AllowAlien = value;

    protected void ConfigurationReadingReturnsError(string error) =>
        _configurationReadingResults.Add(
            ConfigurationReadingResult<ClusterConfiguration>.Error(error)
        );

    protected void ConfigurationReadingReturnsTwoNodes() =>
        _configurationReadingResults.Add(TestConstants.TwoNodesClusterConfiguration);

    protected void FreeSpaceReturns(params ulong[] values)
    {
        foreach (var v in values)
            _freeSpaceResults.Add(v);
    }

    protected void OccupiedSpaceReturns(params ulong[] values)
    {
        foreach (var v in values)
            _occupiedSpaceResults.Add(v);
    }

    protected void DeletePartitionReturns(BobApiResult<bool> response)
    {
        _deletePartitionByIdResults.Add(response);
    }

    protected void NumberOfReturnedPartitionsIs(int count) =>
        PartitionSlimsReturns(
            Enumerable.Range(0, count).Select(_ => new PartitionSlim()).ToArray()
        );

    protected void NumberOfReturnedAlienPartitionsIs(int count) =>
        AlienPartitionSlimsReturns(
            Enumerable.Range(0, count).Select(_ => new PartitionSlim()).ToArray()
        );

    protected void PartitionSlimsReturns(params PartitionSlim[] partitionSlims) =>
        PartitionSlimsReturnsResponse(partitionSlims.ToList());

    protected void AlienPartitionSlimsReturns(params PartitionSlim[] partitionSlims) =>
        AlienPartitionSlimsReturnsResponse(partitionSlims.ToList());

    protected void PartitionSlimsReturnsResponse(BobApiResult<List<PartitionSlim>> response) =>
        _partitionSlimsResults.Add(response);

    protected void AlienPartitionSlimsReturnsResponse(BobApiResult<List<PartitionSlim>> response) =>
        _alienPartitionSlimsResults.Add(response);

    protected void FreeSpaceReturns(BobApiResult<ulong> response) =>
        _freeSpaceResults.Add(response);

    protected void SetAllPartitionsTimestamp(long timestamp) =>
        _allPartitionsDefaultTimestamp = timestamp;

    protected override Result<int> GetResult()
    {
        return _result!;
    }

    protected override IPartitionsBobApiClient GetPartitionsBobApiClientMock()
    {
        return _partitionsBobApiClient;
    }

    private Func<T> CreateReturner<T>(List<T> sequence, T def, Func<T, T>? postprocess = null)
    {
        var ind = 0;
        return () =>
        {
            var result = def;

            if (ind < sequence.Count)
                result = sequence[ind++];
            else if (sequence.Count > 0) // Return last if any exist
                result = sequence.Last();

            if (postprocess != null)
                result = postprocess(result);
            return result;
        };
    }

    private BobApiResult<List<PartitionSlim>> ApplyDefaultValues(
        BobApiResult<List<PartitionSlim>> response
    )
    {
        response = response.Map(
            l =>
                l.All(p => p.Id != null)
                    ? l
                    : l.Select(
                            p =>
                                new PartitionSlim
                                {
                                    Id = p.Id ?? Guid.NewGuid().ToString(),
                                    Timestamp = p.Timestamp
                                }
                        )
                        .ToList()
        );
        if (_allPartitionsDefaultTimestamp != null)
            response = response.Map(
                l =>
                    l.Select(
                            p =>
                                new PartitionSlim
                                {
                                    Id = p.Id,
                                    Timestamp = _allPartitionsDefaultTimestamp.Value
                                }
                        )
                        .ToList()
            );
        return response;
    }
}
