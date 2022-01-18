using System;
using System.Threading;
using System.Threading.Tasks;
using BobToolsCli.ConfigurationFinding;
using OldPartitionsRemover.Entities;

namespace OldPartitionsRemover.BySpaceRemoving
{
    public class Remover
    {
        private readonly Arguments _arguments;
        private readonly IConfigurationFinder _configurationFinder;

        public Remover(Arguments arguments, IConfigurationFinder configurationFinder)
        {
            _arguments = arguments;
            _configurationFinder = configurationFinder;
        }

        public async Task<Result<bool>> RemovePartitionsBySpace(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}