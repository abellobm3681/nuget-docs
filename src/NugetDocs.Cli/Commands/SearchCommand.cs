using System.CommandLine;

namespace NugetDocs.Cli.Commands;

internal sealed class SearchCommand : Command
{
    public Argument<string> PackageArgument { get; } = CommonOptions.Package;
    public Argument<string> PatternArgument { get; } = new("pattern")
    {
        Description = "Search pattern (supports * and ? wildcards)",
    };
    public Option<string?> VersionOption { get; } = CommonOptions.Version;
    public Option<string?> FrameworkOption { get; } = CommonOptions.Framework;

    public SearchCommand() : base("search", "Search types and members by pattern")
    {
        Arguments.Add(PackageArgument);
        Arguments.Add(PatternArgument);
        Options.Add(VersionOption);
        Options.Add(FrameworkOption);

        Action = new SearchCommandAction(this);
    }
}
