using System;
using System.Text.RegularExpressions;
using BobToolsCli;
using CommandLine;
using OldPartitionsRemover.Entites;

namespace OldPartitionsRemover.ByDateRemoving
{
    [Verb("by-date")]
    public class Arguments : CommonArguments
    {
        private static readonly Regex s_daysOffsetRegex = new(@"^\-(\d+)d$");

        [Option('t', "threshold", HelpText = "Removal threshold. Can be either date or in relative days count format, e.g. \"-3d\"", Required = true)]
        public string ThresholdString { get; set; }

        internal Result<DateTimeOffset> GetThreshold()
        {
            if (string.IsNullOrWhiteSpace(ThresholdString))
                return Result<DateTimeOffset>.Error("Removal threshold not set");
            if (s_daysOffsetRegex.IsMatch(ThresholdString))
                return Result<DateTimeOffset>.Ok(DateTime.Now - TimeSpan.FromDays(
                    int.Parse(s_daysOffsetRegex.Match(ThresholdString).Groups[1].Value)));
            else if (DateTime.TryParse(ThresholdString, out var dateTimeThreshold))
                return Result<DateTimeOffset>.Ok(dateTimeThreshold);

            return Result<DateTimeOffset>.Error("Failed to parse threshold");
        }
    }
}