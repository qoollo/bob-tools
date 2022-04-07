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
        private static readonly Regex s_timeSpanRegex = new(@"^\-(?<span>\d+)(?<unit>[dhmy])");

        [Option('t', "threshold", HelpText = "Removal threshold. Can be either date, timestamp or in relative days count format, e.g. \"-3d\"", Required = true)]
        public string ThresholdString { get; set; }

        public Result<DateTime> GetThreshold()
        {
            if (string.IsNullOrWhiteSpace(ThresholdString))
                return Result<DateTime>.Error("Removal threshold not set");
            if (TryParseThreshold(ThresholdString, DateTime.Now, out var threshold))
                return Result<DateTime>.Ok(threshold);
            else if (DateTime.TryParse(ThresholdString, out var dateTimeThreshold))
                return Result<DateTime>.Ok(dateTimeThreshold);
            else if (long.TryParse(ThresholdString, out var l))
                return Result<DateTime>.Ok(DateTimeOffset.FromUnixTimeSeconds(l).DateTime);

            return Result<DateTime>.Error("Failed to parse threshold");
        }

        private static bool TryParseThreshold(string s, DateTime now, out DateTime threshold)
        {
            threshold = now;
            var match = s_timeSpanRegex.Match(s);
            if (match.Success && int.TryParse(match.Groups["span"].Value, out var span))
            {
                span = -span;
                var unit = match.Groups["unit"].Value[0];
                threshold = unit switch
                {
                    'd' => now.AddDays(span),
                    'h' => now.AddHours(span),
                    'm' => now.AddMonths(span),
                    'y' => now.AddYears(span),
                    var c => throw new InvalidOperationException($"Unknown specifier, {c}")
                };
                return true;
            }
            return false;
        }
    }
}