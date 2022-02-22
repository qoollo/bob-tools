using System;
using System.Text.RegularExpressions;
using BobToolsCli;
using ByteSizeLib;
using CommandLine;
using OldPartitionsRemover.Entities;

namespace OldPartitionsRemover.BySpaceRemoving
{
    [Verb("by-space")]
    public class Arguments : CommonArguments
    {
        [Option('t', "threshold", HelpText = "Removal threshold, min space to preserve on each node", Required = true)]
        public string ThresholdString { get; set; }

        [Option('d', "delay", Default = 5, HelpText = "Delay in seconds before each subsequent size request", Required = false)]
        public int DelaySeconds { get; set; }

        public Result<ByteSize> GetThreshold()
        {
            if (string.IsNullOrWhiteSpace(ThresholdString))
                return Result<ByteSize>.Error("Removal threshold not set");
            if (ByteSize.TryParse(ThresholdString, out var bs))
                return Result<ByteSize>.Ok(bs);
            return Result<ByteSize>.Error("Failed to parse threshold");
        }
    }
}