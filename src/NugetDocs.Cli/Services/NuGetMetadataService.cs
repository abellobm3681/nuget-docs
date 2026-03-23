using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NugetDocs.Cli.Services;

/// <summary>
/// Queries the NuGet V3 registration API for package metadata (deprecation, vulnerabilities).
/// Results are cached in-memory for the process lifetime to avoid repeated API calls.
/// </summary>
internal static class NuGetMetadataService
{
    private static readonly ConcurrentDictionary<string, Dictionary<string, VersionMetadata>> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Get deprecation and vulnerability info for all versions of a package.
    /// Results are cached per package ID for the process lifetime.
    /// </summary>
    public static async Task<Dictionary<string, VersionMetadata>> GetVersionMetadataAsync(
        string packageId,
        CancellationToken cancellationToken)
    {
#pragma warning disable CA1308 // NuGet API requires lowercase
        var id = packageId.ToLowerInvariant();
#pragma warning restore CA1308

        if (Cache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var result = new Dictionary<string, VersionMetadata>(StringComparer.OrdinalIgnoreCase);

        using var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip,
            CheckCertificateRevocationList = true,
        };
        using var http = new HttpClient(handler);

        var indexUrl = $"https://api.nuget.org/v3/registration5-gz-semver2/{id}/index.json";
        var index = await http.GetFromJsonAsync<RegistrationIndex>(indexUrl, cancellationToken).ConfigureAwait(false);
        if (index?.Items is null)
        {
            return result;
        }

        foreach (var page in index.Items)
        {
            var items = page.Items;

            // If items are not inline, fetch the page
            if (items is null && page.Id is not null)
            {
                try
                {
                    var pageData = await http.GetFromJsonAsync<RegistrationPage>(
                        page.Id, cancellationToken).ConfigureAwait(false);
                    items = pageData?.Items;
                }
                catch
                {
                    continue; // Skip pages we can't fetch
                }
            }

            if (items is null)
            {
                continue;
            }

            foreach (var item in items)
            {
                var entry = item.CatalogEntry;
                if (entry?.Version is null)
                {
                    continue;
                }

                var meta = new VersionMetadata();

                if (entry.Deprecation is not null)
                {
                    meta.IsDeprecated = true;
                    meta.DeprecationReasons = entry.Deprecation.Reasons;
                    meta.AlternatePackageId = entry.Deprecation.AlternatePackage?.Id;
                    meta.DeprecationMessage = entry.Deprecation.Message;
                }

                if (entry.Vulnerabilities is { Count: > 0 })
                {
                    meta.HasVulnerabilities = true;
                    meta.Vulnerabilities = entry.Vulnerabilities
                        .Select(v => new VulnerabilityInfo(
                            v.AdvisoryUrl,
                            MapSeverity(v.Severity ?? "Unknown")))
                        .ToList();
                }

                if (meta.IsDeprecated || meta.HasVulnerabilities)
                {
                    result[entry.Version] = meta;
                }
            }
        }

        Cache.TryAdd(id, result);
        return result;
    }

    /// <summary>
    /// Get deprecation and vulnerability info for a specific version.
    /// </summary>
    public static async Task<VersionMetadata?> GetVersionMetadataAsync(
        string packageId,
        string version,
        CancellationToken cancellationToken)
    {
        var allMetadata = await GetVersionMetadataAsync(packageId, cancellationToken).ConfigureAwait(false);
        return allMetadata.GetValueOrDefault(version);
    }

    private static string MapSeverity(string severity)
    {
        // NuGet API returns severity as "0", "1", "2", "3" or descriptive names
        return severity switch
        {
            "0" => "Low",
            "1" => "Moderate",
            "2" => "High",
            "3" => "Critical",
            _ => severity,
        };
    }

    internal sealed class VersionMetadata
    {
        public bool IsDeprecated { get; set; }
        public List<string>? DeprecationReasons { get; set; }
        public string? AlternatePackageId { get; set; }
        public string? DeprecationMessage { get; set; }
        public bool HasVulnerabilities { get; set; }
        public List<VulnerabilityInfo>? Vulnerabilities { get; set; }

        public string FormatShort()
        {
            var parts = new List<string>();

            if (IsDeprecated)
            {
                var reasons = DeprecationReasons is { Count: > 0 }
                    ? string.Join(", ", DeprecationReasons)
                    : "deprecated";
                parts.Add($"deprecated: {reasons}");
                if (AlternatePackageId is not null)
                {
                    parts.Add($"use {AlternatePackageId}");
                }
            }

            if (HasVulnerabilities && Vulnerabilities is not null)
            {
                var maxSeverity = Vulnerabilities
                    .Select(v => v.Severity)
                    .OrderByDescending(s => s switch
                    {
                        "Critical" => 3,
                        "High" => 2,
                        "Moderate" => 1,
                        _ => 0,
                    })
                    .First();
                parts.Add($"vulnerability: {maxSeverity}");
            }

            return string.Join(", ", parts);
        }
    }

    internal sealed record VulnerabilityInfo(string? AdvisoryUrl, string Severity);

    // JSON model classes for NuGet V3 Registration API

#pragma warning disable CA1812 // Instantiated via JSON deserialization

    private sealed class RegistrationIndex
    {
        [JsonPropertyName("items")]
        public List<RegistrationPage>? Items { get; set; }
    }

    private sealed class RegistrationPage
    {
        [JsonPropertyName("@id")]
        public string? Id { get; set; }

        [JsonPropertyName("items")]
        public List<RegistrationLeaf>? Items { get; set; }
    }

    private sealed class RegistrationLeaf
    {
        [JsonPropertyName("catalogEntry")]
        public CatalogEntry? CatalogEntry { get; set; }
    }

    private sealed class CatalogEntry
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("deprecation")]
        public DeprecationInfo? Deprecation { get; set; }

        [JsonPropertyName("vulnerabilities")]
        public List<VulnerabilityEntry>? Vulnerabilities { get; set; }
    }

    private sealed class DeprecationInfo
    {
        [JsonPropertyName("reasons")]
        public List<string>? Reasons { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("alternatePackage")]
        public AlternatePackageInfo? AlternatePackage { get; set; }
    }

    private sealed class AlternatePackageInfo
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("range")]
        public string? Range { get; set; }
    }

    private sealed class VulnerabilityEntry
    {
        [JsonPropertyName("advisoryUrl")]
        public string? AdvisoryUrl { get; set; }

        [JsonPropertyName("severity")]
        public string? Severity { get; set; }
    }

#pragma warning restore CA1812
}
