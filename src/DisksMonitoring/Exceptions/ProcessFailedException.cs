using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DisksMonitoring.Exceptions
{
    class ProcessFailedException : Exception
    {
        private readonly ProcessStartInfo processStartInfo;

        public ProcessFailedException(ProcessStartInfo processStartInfo)
        {
            this.processStartInfo = processStartInfo;
        }
    }
}
