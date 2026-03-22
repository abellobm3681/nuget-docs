using System.CommandLine;

namespace NugetDocs.Cli.Commands;

internal sealed class InfoCommand : Command
{
    public Argument<string> PackageArgument { get; } = CommonOptions.Package;
    public Option<string?> VersionOption { get; } = CommonOptions.Version;

    public InfoCommand() : base("info", "Show package metadata from .nuspec")
    {
        Arguments.Add(PackageArgument);
        Options.Add(VersionOption);

        Action = new InfoCommandAction(this);
    }
}
