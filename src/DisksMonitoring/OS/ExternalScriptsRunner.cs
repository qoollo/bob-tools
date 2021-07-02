using DisksMonitoring.Config;
using DisksMonitoring.Entities;
using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksFinding.Entities;
using DisksMonitoring.OS.DisksProcessing.Entities;
using DisksMonitoring.OS.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisksMonitoring.OS
{
    internal class ExternalScriptsRunner
    {
        private readonly IList<string> preCycleScripts;
        private readonly IList<string> postCycleScripts;
        private readonly ProcessInvoker processInvoker;
        private readonly ILogger<ExternalScriptsRunner> logger;

        public ExternalScriptsRunner(Configuration configuration, ProcessInvoker processInvoker, ILogger<ExternalScriptsRunner> logger)
        {
            this.preCycleScripts = configuration.PreCycleScripts;
            this.postCycleScripts = configuration.PostCycleScripts;
            this.processInvoker = processInvoker;
            this.logger = logger;
        }

        public async Task RunPreCycleScripts()
        {
            foreach (var script in preCycleScripts.Where(File.Exists))
            {
                logger.LogDebug($"Running pre-script {script}");
                await RunScript(script);
            }
        }

        public async Task RunPostCycleScripts()
        {
            foreach (var script in postCycleScripts.Where(File.Exists))
            {
                logger.LogDebug($"Running post-script {script}");
                await RunScript(script);
            }
        }

        private async Task RunScript(string script) => await processInvoker.InvokeSudoProcess("bash", "-c", script);
    }
}