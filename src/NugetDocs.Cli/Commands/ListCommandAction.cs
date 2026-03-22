using System.CommandLine;
using System.CommandLine.Invocation;
using NugetDocs.Cli.Services;

namespace NugetDocs.Cli.Commands;

internal sealed class ListCommandAction(ListCommand command) : AsynchronousCommandLineAction
{
    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        var package = parseResult.GetValue(command.PackageArgument)!;
        var version = parseResult.GetValue(command.VersionOption);
        var framework = parseResult.GetValue(command.FrameworkOption);

        try
        {
            var resolved = await PackageResolver.ResolveAsync(
                package, version, framework, cancellationToken).ConfigureAwait(false);

            using var inspector = new TypeInspector(resolved.DllPath, resolved.XmlDocPath);
            var xmlDocs = XmlDocReader.TryLoad(resolved.XmlDocPath);
            var types = inspector.GetPublicTypes();

            Console.WriteLine($"Package: {resolved.PackageId} {resolved.Version} ({resolved.Framework})");
            Console.WriteLine();

            // Group by kind
            var grouped = types.GroupBy(t => t.Kind).OrderBy(g => GetKindOrder(g.Key));

            foreach (var group in grouped)
            {
                var pluralKey = group.Key.EndsWith('s') ? $"{group.Key}es" : $"{group.Key}s";
                Console.WriteLine($"{pluralKey}:");

                foreach (var type in group)
                {
                    var displayName = type.GenericParameterCount > 0
                        ? $"{type.Name}<{new string(',', type.GenericParameterCount - 1)}>"
                        : type.Name;

                    var summary = xmlDocs?.GetTypeSummary(type.FullName);
                    var line = summary is not null
                        ? $"  {displayName} — {summary}"
                        : $"  {displayName}";

                    Console.WriteLine(line);
                }

                Console.WriteLine();
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int GetKindOrder(string kind) => kind switch
    {
        "Interface" => 0,
        "Class" => 1,
        "Struct" => 2,
        "Enum" => 3,
        "Delegate" => 4,
        _ => 5,
    };
}
