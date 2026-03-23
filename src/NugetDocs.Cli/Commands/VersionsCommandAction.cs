using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NugetDocs.Cli.Commands;

internal sealed class VersionsCommandAction(VersionsCommand command) : AsynchronousCommandLineAction
{
    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        var package = parseResult.GetValue(command.PackageArgument)!;
        var stableOnly = parseResult.GetValue(command.StableOption);
        var latest = parseResult.GetValue(command.LatestOption);
        var limit = parseResult.GetValue(command.LimitOption);
        var output = parseResult.GetValue(command.OutputOption);

        try
        {
            var packageId = package.ToLowerInvariant();
            using var http = new HttpClient();
            var url = $"https://api.nuget.org/v3-flatcontainer/{packageId}/index.json";
            var response = await http.GetFromJsonAsync<VersionIndex>(url, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Could not resolve versions for package '{package}'.");

            var versions = response.Versions ?? [];

            if (stableOnly)
            {
                versions = versions.Where(v => !v.Contains('-')).ToList();
            }

            // Show newest first
            versions.Reverse();

            if (latest)
            {
                var latestStable = versions.FirstOrDefault(v => !v.Contains('-'));
                var latestPrerelease = versions.FirstOrDefault(v => v.Contains('-'));
                versions = new[] { latestStable, latestPrerelease }
                    .Where(v => v is not null)
                    .Cast<string>()
                    .ToList();
            }

            var total = versions.Count;
            if (!latest && limit > 0 && versions.Count > limit)
            {
                versions = versions.Take(limit).ToList();
            }

            if (string.Equals(output, "json", StringComparison.OrdinalIgnoreCase))
            {
                var json = latest
                    ? new
                    {
                        package,
                        total,
                        stableOnly,
                        latestStable = versions.FirstOrDefault(v => !v.Contains('-')),
                        latestPrerelease = versions.FirstOrDefault(v => v.Contains('-')),
                        versions,
                    }
                    : (object)new
                    {
                        package,
                        total,
                        stableOnly,
                        versions,
                    };
                Console.WriteLine(JsonSerializer.Serialize(json, JsonOptions.Indented));
            }
            else
            {
                var filter = latest ? " (latest)" : stableOnly ? " (stable only)" : "";
                Console.WriteLine($"// Versions: {package}{filter}");
                if (!latest)
                {
                    Console.WriteLine($"// Total: {total}");
                }
                Console.WriteLine();

                if (latest)
                {
                    var latestStable = versions.FirstOrDefault(v => !v.Contains('-'));
                    var latestPrerelease = versions.FirstOrDefault(v => v.Contains('-'));
                    if (latestStable is not null)
                    {
                        Console.WriteLine($"  {latestStable}  (stable)");
                    }
                    if (latestPrerelease is not null)
                    {
                        Console.WriteLine($"  {latestPrerelease}  (prerelease)");
                    }
                }
                else
                {
                    foreach (var v in versions)
                    {
                        Console.WriteLine($"  {v}");
                    }
                }

                if (limit > 0 && total > limit)
                {
                    Console.WriteLine();
                    Console.WriteLine($"  ... and {total - limit} more (use --limit 0 to show all)");
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

    private sealed class VersionIndex
    {
        [JsonPropertyName("versions")]
        public List<string>? Versions { get; set; }
    }
}
