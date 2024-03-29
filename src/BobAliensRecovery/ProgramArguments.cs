using BobToolsCli;
using CommandLine;

namespace BobAliensRecovery
{
    class ProgramArguments : CommonWithSshArguments
    {
        [Option("remove-copied", HelpText = "Remove copied blobs", Default = false)]
        public bool RemoveCopied { get; set; }

        [Option("restart-nodes", HelpText = "Restart nodes after aliens have been copied", Default = false)]
        public bool RestartNodes { get; set; }

        [Option("copy-parallel-degree", HelpText = "Number of simultaneous copy processes", Default = 1)]
        public int CopyParallelDegree { get; set; }

        [Option("hash-parallel-degree", HelpText = "Number of simultaneous hash processes. Note: there will be at most one process per node", Default = 1)]
        public int HashParallelDegree { get; set; }

        public AliensRecoveryOptions AliensRecoveryOptions
            => new(RemoveCopied, ContinueOnError, RestartNodes, CopyParallelDegree, HashParallelDegree);
    }
}
