using System.CommandLine;
using NugetDocs.Cli.Commands;

namespace NugetDocs.IntegrationTests;

internal static class CliTestHelper
{
    /// <summary>
    /// Runs the CLI with the given arguments and returns (exitCode, stdout, stderr).
    /// </summary>
    public static async Task<(int ExitCode, string Output, string Error)> RunAsync(params string[] args)
    {
        var rootCommand = new RootCommand("Inspect public API documentation from any NuGet package")
        {
            new ListCommand(),
            new ShowCommand(),
            new SearchCommand(),
            new InfoCommand(),
            new DepsCommand(),
            new VersionsCommand(),
            new DiffCommand(),
        };

        var stdOut = new StringWriter();
        var stdErr = new StringWriter();

        var originalOut = Console.Out;
        var originalErr = Console.Error;

        try
        {
            Console.SetOut(stdOut);
            Console.SetError(stdErr);

            var exitCode = await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);

            return (exitCode, stdOut.ToString(), stdErr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }
}
