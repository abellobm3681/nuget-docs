using System.CommandLine;

namespace NugetDocs.Cli.Commands;

internal sealed class VersionsCommand : Command
{
    public Argument<string> PackageArgument { get; } = CommonOptions.Package;
    public Option<bool> StableOption { get; } = new("--stable", "-s")
    {
        Description = "Show only stable versions (exclude prereleases)",
        DefaultValueFactory = _ => false,
    };
    public Option<int> LimitOption { get; } = new("--limit", "-l")
    {
        Description = "Maximum number of versions to show (default: 20, 0 = all)",
        DefaultValueFactory = _ => 20,
    };
    public Option<string?> OutputOption { get; } = CommonOptions.Output;

    public VersionsCommand() : base("versions", "List all available versions of a package from NuGet.org")
    {
        Arguments.Add(PackageArgument);
        Options.Add(StableOption);
        Options.Add(LimitOption);
        Options.Add(OutputOption);

        Action = new VersionsCommandAction(this);
    }
}
