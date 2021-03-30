using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DisksMonitoring.Exceptions
{
    class ProcessFailedException : Exception
    {
        private readonly ProcessStartInfo processStartInfo;

        public ProcessFailedException(ProcessStartInfo processStartInfo, int exitCode)
        {
            this.processStartInfo = processStartInfo;
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
