using System.CommandLine;
using System.CommandLine.Invocation;
using NugetDocs.Cli.Services;

namespace NugetDocs.Cli.Commands;

internal sealed class SearchCommandAction(SearchCommand command) : AsynchronousCommandLineAction
{
    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        var package = parseResult.GetValue(command.PackageArgument)!;
        var pattern = parseResult.GetValue(command.PatternArgument)!;
        var version = parseResult.GetValue(command.VersionOption);
        var framework = parseResult.GetValue(command.FrameworkOption);

        try
        {
            var resolved = await PackageResolver.ResolveAsync(
                package, version, framework, cancellationToken).ConfigureAwait(false);

            using var inspector = new TypeInspector(resolved.DllPath, resolved.XmlDocPath);
            var results = inspector.SearchTypes(pattern);

            Console.WriteLine($"Package: {resolved.PackageId} {resolved.Version} ({resolved.Framework})");
            Console.WriteLine($"Pattern: {pattern}");
            Console.WriteLine($"Results: {results.Count}");
            Console.WriteLine();

            foreach (var result in results)
            {
                var kindLabel = result.MemberKind is not null
                    ? $"{result.Kind}.{result.MemberKind}"
                    : result.Kind;

                Console.WriteLine($"  [{kindLabel}] {result.FullName}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
