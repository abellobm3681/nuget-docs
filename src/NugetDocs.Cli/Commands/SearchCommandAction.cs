using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
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
        var showAll = parseResult.GetValue(command.AllOption);
        var namespaceFilter = parseResult.GetValue(command.NamespaceOption);
        var format = parseResult.GetValue(command.FormatOption);
        var jsonOutput = CommonOptions.IsJsonOutput(parseResult, command.OutputOption, command.JsonOption);

        try
        {
            var resolved = await PackageResolver.ResolveAsync(
                package, version, framework, cancellationToken).ConfigureAwait(false);

            using var inspector = new TypeInspector(resolved.DllPath, resolved.XmlDocPath);
            var allResults = inspector.SearchTypes(pattern, publicOnly: !showAll);

            var results = namespaceFilter is not null
                ? allResults.Where(r => r.FullName.StartsWith(namespaceFilter, StringComparison.OrdinalIgnoreCase)).ToList()
                : allResults;

            if (jsonOutput)
            {
                var json = new
                {
                    package = resolved.PackageId,
                    version = resolved.Version,
                    framework = resolved.Framework,
                    pattern,
                    count = results.Count,
                    results = results.Select(r => new
                    {
                        kind = r.Kind,
                        memberKind = r.MemberKind,
                        name = r.Name,
                        fullName = r.FullName,
                    }),
                };
                Console.WriteLine(JsonSerializer.Serialize(json, JsonOptions.Indented));
            }
            else if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Kind,MemberKind,Name,FullName");
                foreach (var result in results)
                {
                    Console.WriteLine($"{result.Kind},{result.MemberKind ?? ""},{CsvEscape(result.Name)},{CsvEscape(result.FullName)}");
                }
            }
            else if (string.Equals(format, "table", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Package: {resolved.PackageId} {resolved.Version} ({resolved.Framework})");
                Console.WriteLine($"Pattern: {pattern}");
                Console.WriteLine($"Results: {results.Count}");
                Console.WriteLine();

                var rows = results.Select(r => new
                {
                    Kind = r.MemberKind is not null ? $"{r.Kind}.{r.MemberKind}" : r.Kind,
                    Name = r.Name,
                    FullName = r.FullName,
                }).ToList();

                var colKind = Math.Max("Kind".Length, rows.Count > 0 ? rows.Max(r => r.Kind.Length) : 0);
                var colName = Math.Max("Name".Length, rows.Count > 0 ? rows.Max(r => r.Name.Length) : 0);

                Console.WriteLine($"  {"Kind".PadRight(colKind)}  {"Name".PadRight(colName)}  FullName");
                Console.WriteLine($"  {new string('-', colKind)}  {new string('-', colName)}  --------");

                foreach (var row in rows)
                {
                    Console.WriteLine($"  {row.Kind.PadRight(colKind)}  {row.Name.PadRight(colName)}  {row.FullName}");
                }
            }
            else
            {
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
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',', StringComparison.Ordinal) ||
            value.Contains('"', StringComparison.Ordinal) ||
            value.Contains('\n', StringComparison.Ordinal))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }
        return value;
    }
}
