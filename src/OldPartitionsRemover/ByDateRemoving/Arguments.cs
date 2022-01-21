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

        public Result<DateTimeOffset> GetThreshold()
        {
            if (string.IsNullOrWhiteSpace(ThresholdString))
                return Result<DateTimeOffset>.Error("Removal threshold not set");
            if (TryParseTimeSpan(ThresholdString, out var ts))
                return Result<DateTimeOffset>.Ok(DateTime.Now + ts);
            else if (DateTime.TryParse(ThresholdString, out var dateTimeThreshold))
                return Result<DateTimeOffset>.Ok(dateTimeThreshold);

            return Result<DateTimeOffset>.Error("Failed to parse threshold");
        }

        private static bool TryParseTimeSpan(string s, out TimeSpan ts)
        {
            ts = TimeSpan.Zero;
            var match = s_timeSpanRegex.Match(s);
            if (match.Success)
            {
                var span = int.Parse(match.Groups["span"].Value);
                var unit = match.Groups["unit"].Value[0];
                Func<double, TimeSpan> creator = unit switch
                {
                    'd' => TimeSpan.FromDays,
                    'h' => TimeSpan.FromHours,
                    'm' => x => TimeSpan.FromDays(x * 30),
                    'y' => x => TimeSpan.FromDays(365 * x),
                    var c => throw new InvalidOperationException($"Unknown specifier, {c}")
                };
                ts = -creator(span);
                return true;
            }
            return false;
        }
    }
}