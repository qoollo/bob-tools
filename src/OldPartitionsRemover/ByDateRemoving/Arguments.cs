using System;
using System.Globalization;
using System.Text.RegularExpressions;
using BobToolsCli;
using CommandLine;
using OldPartitionsRemover.Entities;

namespace OldPartitionsRemover.ByDateRemoving
{
    [Verb("by-date")]
    public class Arguments : CommonArguments
    {
        private static readonly Regex s_daysOffsetRegex = new(@"^\-(\d+)d$");
        private static readonly Regex s_timeSpanRegex = new(@"^\-(?<span>\d+)(?<unit>[dhmy])");

        [Option('t', "threshold", HelpText = "Removal threshold. Can be either date or in relative days count format, e.g. \"-3d\"", Required = true)]
        public string ThresholdString { get; set; }

        public Result<DateTime> GetThreshold()
        {
            if (string.IsNullOrWhiteSpace(ThresholdString))
                return Result<DateTime>.Error("Removal threshold not set");
            if (TryParseThreshold(ThresholdString, DateTime.Now, out var threshold))
                return Result<DateTime>.Ok(threshold);
            else if (DateTime.TryParse(ThresholdString, out var dateTimeThreshold))
                return Result<DateTime>.Ok(dateTimeThreshold);

            return Result<DateTime>.Error("Failed to parse threshold");
        }

        private static bool TryParseThreshold(string s, DateTime now, out DateTime threshold)
        {
            threshold = now;
            var match = s_timeSpanRegex.Match(s);
            if (match.Success)
            {
                var span = -int.Parse(match.Groups["span"].Value);
                var unit = match.Groups["unit"].Value[0];
                threshold = unit switch
                {
                    'd' => now.AddDays(span),
                    'h' => now.AddHours(span),
                    'm' => now.AddMonths(span),
                    'y' => now.AddMonths(span * 12),
                    var c => throw new InvalidOperationException($"Unknown specifier, {c}")
                };
                return true;
            }
            return false;
        }
    }
}