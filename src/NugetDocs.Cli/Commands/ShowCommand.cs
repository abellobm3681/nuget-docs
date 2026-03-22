using System.CommandLine;

namespace NugetDocs.Cli.Commands;

internal sealed class ShowCommand : Command
{
    public Argument<string> PackageArgument { get; } = CommonOptions.Package;
    public Argument<string> TypeArgument { get; } = new("type")
    {
        Description = "Type name (short name like IChatClient or full name)",
    };
    public Option<string?> VersionOption { get; } = CommonOptions.Version;
    public Option<string?> FrameworkOption { get; } = CommonOptions.Framework;

    public ShowCommand() : base("show", "Show decompiled source for a specific type with XML documentation")
    {
        Arguments.Add(PackageArgument);
        Arguments.Add(TypeArgument);
        Options.Add(VersionOption);
        Options.Add(FrameworkOption);

        Action = new ShowCommandAction(this);
    }
}
