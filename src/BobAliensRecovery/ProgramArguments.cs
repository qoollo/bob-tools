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

        [Option("hash-parallel", HelpText = "Run hash calculations in parallel", Default = true)]
        public bool ParallelHash { get; set; }

        public AliensRecoveryOptions AliensRecoveryOptions
            => new(RemoveCopied, ContinueOnError, RestartNodes, CopyParallelDegree);
    }
}
