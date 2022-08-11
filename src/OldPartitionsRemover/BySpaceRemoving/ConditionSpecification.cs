using System;
using System.Threading;
using System.Threading.Tasks;
using BobApi;
using BobApi.BobEntities;
using BobToolsCli.BobApliClientFactories;
using ByteSizeLib;
using Microsoft.Extensions.Logging;
using OldPartitionsRemover.Entities;

namespace OldPartitionsRemover.BySpaceRemoving
{
    public class ConditionSpecification
    {
        private readonly ByteSize _threshold;
        private readonly string _thresholdType;

        public ConditionSpecification(ByteSize threshold, string thresholdType)
        {
            _threshold = threshold;
            _thresholdType = thresholdType;
        }

        public NodeConditionSpecification GetForNode(IBobApiClientFactory bobApiClientFactory,
            ClusterConfiguration.Node node)
        {
            if (_thresholdType.Equals("free", StringComparison.OrdinalIgnoreCase))
                return GetFreeSpaceCondition(bobApiClientFactory.GetSpaceBobApiClient(node), node);
            else if (_thresholdType.Equals("occupied", StringComparison.OrdinalIgnoreCase))
                return GetOccupiedSpaceCondition(bobApiClientFactory.GetSpaceBobApiClient(node), node);
            throw new NotImplementedException();
        }

        private NodeConditionSpecification GetFreeSpaceCondition(ISpaceBobApiClient client, ClusterConfiguration.Node node)
        {
            return new NodeConditionSpecification(node,
                async t => await client.GetFreeSpaceBytes(t),
                s => s > _threshold);
        }

        private NodeConditionSpecification GetOccupiedSpaceCondition(ISpaceBobApiClient client, ClusterConfiguration.Node node)
        {
            return new NodeConditionSpecification(node,
                async t => await client.GetOccupiedSpaceBytes(t),
                s => s <= _threshold);
        }
    }

    public class NodeConditionSpecification
    {
        private ulong _minSpace = ulong.MaxValue;
        private ulong _maxSpace = ulong.MinValue;
        private readonly Func<CancellationToken, Task<Result<ulong>>> _getCurrentBytes;
        private readonly Func<ByteSize, bool> _isDone;

        public NodeConditionSpecification(ClusterConfiguration.Node node,
            Func<CancellationToken, Task<Result<ulong>>> getCurrentBytes,
            Func<ByteSize, bool> isDone)
        {
            Node = node;
            _getCurrentBytes = getCurrentBytes;
            _isDone = isDone;
        }

        public ClusterConfiguration.Node Node { get; }

        public async Task<Result<bool>> CheckIsDone(ILogger logger, CancellationToken cancellationToken)
        {
            var spaceResult = await _getCurrentBytes(cancellationToken);
            return spaceResult.Map(space =>
            {
                if (space < _minSpace)
                    _minSpace = space;
                if (space > _maxSpace)
                    _maxSpace = space;
                var s = ByteSize.FromBytes(space);
                logger.LogTrace("Current space is {Space}", s);
                return _isDone(s);
            });
        }

        public string GetChangeString()
        {
            if (_minSpace == ulong.MaxValue && _maxSpace == ulong.MinValue)
                return "nothing freed";
            return $"freed: {ByteSize.FromBytes(_maxSpace - _minSpace)}";
        }
    }
}
