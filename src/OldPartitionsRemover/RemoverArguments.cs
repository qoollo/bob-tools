using BobToolsCli;
using CommandLine;

namespace OldPartitionsRemover;

public class RemoverArguments : CommonArguments
{
    [Option(
        'a',
        "allow-alien",
        Default = false,
        HelpText = "Allow removal of alien partitions",
        Required = false
    )]
    public bool AllowAlien { get; set; }
}
