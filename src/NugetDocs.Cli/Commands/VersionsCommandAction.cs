using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using NugetDocs.Cli.Services;
using static NugetDocs.Cli.Services.NuGetMetadataService;

namespace NugetDocs.Cli.Commands;

internal sealed class VersionsCommandAction(VersionsCommand command) : AsynchronousCommandLineAction
{
    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        var package = parseResult.GetValue(command.PackageArgument)!;
        var stableOnly = parseResult.GetValue(command.StableOption);
        var prereleaseOnly = parseResult.GetValue(command.PrereleaseOption);
        var latest = parseResult.GetValue(command.LatestOption);
        var since = parseResult.GetValue(command.SinceOption);
        var limit = parseResult.GetValue(command.LimitOption);
        var count = parseResult.GetValue(command.CountOption);
        var showDeprecated = parseResult.GetValue(command.DeprecatedOption);
        var format = parseResult.GetValue(command.FormatOption);
        var jsonOutput = CommonOptions.IsJsonOutput(parseResult, command.OutputOption, command.JsonOption);

        try
        {
            using var http = new HttpClient();
#pragma warning disable CA1308 // NuGet API requires lowercase package names
            var url = $"https://api.nuget.org/v3-flatcontainer/{package.ToLowerInvariant()}/index.json";
#pragma warning restore CA1308
            var response = await http.GetFromJsonAsync<VersionIndex>(url, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Could not resolve versions for package '{package}'.");

            var versions = response.Versions ?? [];

            if (stableOnly)
            {
                versions = versions.Where(v => !IsPrerelease(v)).ToList();
            }
            else if (prereleaseOnly)
            {
                versions = versions.Where(IsPrerelease).ToList();
            }

            if (since is not null)
            {
#pragma warning disable CA1308 // NuGet API requires lowercase package names
                var resolvedSince = PackageResolver.IsVersionKeyword(since)
                    ? await PackageResolver.ResolveVersionKeywordAsync(
                        package.ToLowerInvariant(), since, cancellationToken).ConfigureAwait(false)
                    : since;
#pragma warning restore CA1308

                var sinceIndex = versions.IndexOf(resolvedSince);
                if (sinceIndex >= 0)
                {
                    versions = versions.Skip(sinceIndex + 1).ToList();
                    since = resolvedSince; // Use resolved version in output
                }
                else
                {
                    Console.Error.WriteLine($"Warning: Version '{resolvedSince}' not found in package history. Showing all versions.");
                }
            }

            // Show newest first
            versions.Reverse();

            if (latest)
            {
                var latestStable = versions.FirstOrDefault(v => !IsPrerelease(v));
                var latestPrerelease = versions.FirstOrDefault(IsPrerelease);
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

            // Fetch deprecation/vulnerability metadata if requested
            Dictionary<string, VersionMetadata>? metadata = null;
            if (showDeprecated)
            {
                metadata = await NuGetMetadataService.GetVersionMetadataAsync(
                    package, cancellationToken).ConfigureAwait(false);
            }

            if (count)
            {
                if (jsonOutput)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { package, count = total }, JsonOptions.Indented));
                }
                else
                {
                    Console.WriteLine(total);
                }
                return 0;
            }

            if (jsonOutput)
            {
                if (showDeprecated && metadata is not null)
                {
                    var versionEntries = versions.Select(v =>
                    {
                        metadata.TryGetValue(v, out var meta);
                        return new
                        {
                            version = v,
                            deprecated = meta?.IsDeprecated ?? false,
                            deprecationReasons = meta?.DeprecationReasons,
                            alternatePackage = meta?.AlternatePackageId,
                            vulnerabilities = meta?.Vulnerabilities?.Select(vul => new
                            {
                                severity = vul.Severity,
                                advisoryUrl = vul.AdvisoryUrl,
                            }),
                        };
                    });

                    var json = latest
                        ? (object)new
                        {
                            package,
                            total,
                            stableOnly,
                            latestStable = versions.FirstOrDefault(v => !IsPrerelease(v)),
                            latestPrerelease = versions.FirstOrDefault(IsPrerelease),
                            versions = versionEntries,
                        }
                        : new
                        {
                            package,
                            total,
                            stableOnly,
                            latestStable = (string?)null,
                            latestPrerelease = (string?)null,
                            versions = versionEntries,
                        };
                    Console.WriteLine(JsonSerializer.Serialize(json, JsonOptions.Indented));
                }
                else
                {
                    var json = latest
                        ? new
                        {
                            package,
                            total,
                            stableOnly,
                            latestStable = versions.FirstOrDefault(v => !IsPrerelease(v)),
                            latestPrerelease = versions.FirstOrDefault(IsPrerelease),
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
            }
            else if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                var header = showDeprecated ? "Version,Prerelease,Deprecated,Info" : "Version,Prerelease";
                Console.WriteLine(header);
                foreach (var v in versions)
                {
                    var pre = IsPrerelease(v) ? "true" : "false";
                    if (showDeprecated)
                    {
                        var info = metadata is not null && metadata.TryGetValue(v, out var meta)
                            ? meta.FormatShort() : "";
                        var dep = info.Length > 0 ? "true" : "false";
                        Console.WriteLine($"{CommonOptions.CsvEscape(v)},{pre},{dep},{CommonOptions.CsvEscape(info)}");
                    }
                    else
                    {
                        Console.WriteLine($"{CommonOptions.CsvEscape(v)},{pre}");
                    }
                }
            }
            else if (string.Equals(format, "table", StringComparison.OrdinalIgnoreCase))
            {
                var parts = new List<string>();
                if (latest) parts.Add("latest");
                if (stableOnly) parts.Add("stable only");
                if (prereleaseOnly) parts.Add("prerelease only");
                if (since is not null) parts.Add($"since {since}");
                var filter = parts.Count > 0 ? $" ({string.Join(", ", parts)})" : "";
                Console.WriteLine($"Versions: {package}{filter}");
                if (!latest)
                {
                    Console.WriteLine($"Total: {total}");
                }
                Console.WriteLine();

                var rows = versions.Select(v => new
                {
                    Version = v,
                    Prerelease = IsPrerelease(v) ? "yes" : "",
                    Info = showDeprecated && metadata is not null && metadata.TryGetValue(v, out var meta)
                        ? meta.FormatShort() : "",
                }).ToList();

                var colVer = Math.Max("Version".Length, rows.Count > 0 ? rows.Max(r => r.Version.Length) : 0);
                var colPre = Math.Max("Pre".Length, rows.Count > 0 ? rows.Max(r => r.Prerelease.Length) : 0);

                if (showDeprecated)
                {
                    var colInfo = Math.Max("Info".Length, rows.Count > 0 ? rows.Max(r => r.Info.Length) : 0);
                    Console.WriteLine($"  {"Version".PadRight(colVer)}  {"Pre".PadRight(colPre)}  Info");
                    Console.WriteLine($"  {new string('-', colVer)}  {new string('-', colPre)}  {new string('-', Math.Max(colInfo, 4))}");
                    foreach (var row in rows)
                    {
                        Console.WriteLine($"  {row.Version.PadRight(colVer)}  {row.Prerelease.PadRight(colPre)}  {row.Info}");
                    }
                }
                else
                {
                    Console.WriteLine($"  {"Version".PadRight(colVer)}  Pre");
                    Console.WriteLine($"  {new string('-', colVer)}  {new string('-', Math.Max(colPre, 3))}");
                    foreach (var row in rows)
                    {
                        Console.WriteLine($"  {row.Version.PadRight(colVer)}  {row.Prerelease}");
                    }
                }

                if (limit > 0 && total > limit)
                {
                    Console.WriteLine();
                    Console.WriteLine($"  ... and {total - limit} more (use --limit 0 to show all)");
                }
            }
            else
            {
                var parts = new List<string>();
                if (latest) parts.Add("latest");
                if (stableOnly) parts.Add("stable only");
                if (prereleaseOnly) parts.Add("prerelease only");
                if (since is not null) parts.Add($"since {since}");
                var filter = parts.Count > 0 ? $" ({string.Join(", ", parts)})" : "";
                Console.WriteLine($"// Versions: {package}{filter}");
                if (!latest)
                {
                    Console.WriteLine($"// Total: {total}");
                }
                Console.WriteLine();

                if (latest)
                {
                    var latestStable = versions.FirstOrDefault(v => !IsPrerelease(v));
                    var latestPrerelease = versions.FirstOrDefault(IsPrerelease);
                    if (latestStable is not null)
                    {
                        var marker = GetDeprecationMarker(latestStable, metadata);
                        Console.WriteLine($"  {latestStable}  (stable){marker}");
                    }
                    if (latestPrerelease is not null)
                    {
                        var marker = GetDeprecationMarker(latestPrerelease, metadata);
                        Console.WriteLine($"  {latestPrerelease}  (prerelease){marker}");
                    }
                }
                else
                {
                    foreach (var v in versions)
                    {
                        var marker = GetDeprecationMarker(v, metadata);
                        Console.WriteLine($"  {v}{marker}");
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

    private static string GetDeprecationMarker(string version, Dictionary<string, VersionMetadata>? metadata)
    {
        if (metadata is null || !metadata.TryGetValue(version, out var meta))
        {
            return "";
        }

        return $"  ** {meta.FormatShort()}";
    }

    private static bool IsPrerelease(string version) => version.Contains('-', StringComparison.Ordinal);

#pragma warning disable CA1812 // Instantiated via JSON deserialization
    private sealed class VersionIndex
#pragma warning restore CA1812
    {
        [JsonPropertyName("versions")]
        public List<string>? Versions { get; set; }
    }
}
