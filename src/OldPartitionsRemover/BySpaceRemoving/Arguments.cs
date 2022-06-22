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
        [Option('t', "threshold", HelpText = "Removal threshold", Required = true)]
        public string ThresholdString { get; set; }

        [Option('d', "delay", Default = 300, HelpText = "Delay in milliseconds before each subsequent size request", Required = false)]
        public int DelayMilliseconds { get; set; }

        [Option("threshold-type", Default = "free", HelpText = "Type of threshold: `free` space on node or bob's `occupied` space")]
        public string ThresholdType { get; set; } // Enums are case sensitive in CommandLineParser by default, and changing this requires recreating whole help

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