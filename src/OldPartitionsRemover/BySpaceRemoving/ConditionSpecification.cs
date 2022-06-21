using System;
using System.Threading;
using System.Threading.Tasks;
using BobApi;
using BobApi.BobEntities;
using BobToolsCli.BobApliClientFactories;
using ByteSizeLib;
using OldPartitionsRemover.Entities;

namespace OldPartitionsRemover.BySpaceRemoving
{
    public class ConditionSpecification
    {
        private readonly ByteSize _threshold;

        public ConditionSpecification(ByteSize threshold)
        {
            _threshold = threshold;
        }

        public NodeConditionSpecification GetForNode(IBobApiClientFactory bobApiClientFactory,
            ClusterConfiguration.Node node)
        {
            return new NodeConditionSpecification(node, bobApiClientFactory.GetSpaceBobApiClient(node), _threshold);
            throw new NotImplementedException();
        }
    }

    public class NodeConditionSpecification
    {
        private ulong _minFreeSpace = ulong.MaxValue;
        private ulong _maxFreeSpace = 0;
        private readonly ISpaceBobApiClient _spaceClient;
        private readonly ByteSize _threshold;

        public NodeConditionSpecification(ClusterConfiguration.Node node, ISpaceBobApiClient spaceClient, ByteSize threshold)
        {
            Node = node;
            _spaceClient = spaceClient;
            _threshold = threshold;
        }

        public ClusterConfiguration.Node Node { get; }

        public async Task<Result<bool>> CheckIsDone(CancellationToken cancellationToken)
        {
            Result<ulong> spaceResult = await _spaceClient.GetFreeSpaceBytes(cancellationToken);
            return spaceResult.Map(space =>
            {
                var s = ByteSize.FromBytes(space);
                if (space < _minFreeSpace)
                    _minFreeSpace = space;
                if (space > _maxFreeSpace)
                    _maxFreeSpace = space;
                return s > _threshold;
            });
        }

        public ByteSize GetSpaceStat() => ByteSize.FromBytes(_maxFreeSpace - _minFreeSpace);
    }
}