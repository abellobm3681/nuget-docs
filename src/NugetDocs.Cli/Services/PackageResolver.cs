using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace NugetDocs.Cli.Services;

/// <summary>
/// Resolves NuGet packages to their DLL and XML doc paths.
/// </summary>
internal sealed class PackageResolver
{
    private static readonly string NuGetCacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".nuget", "packages");

    private static readonly string[] TfmPreference =
    [
        "net10.0", "net9.0", "net8.0", "net7.0", "net6.0",
        "netstandard2.1", "netstandard2.0",
    ];

    internal sealed record ResolvedPackage(
        string PackageId,
        string Version,
        string? Framework,
        string? DllPath,
        string? XmlDocPath,
        string PackageDir);

    /// <summary>
    /// Resolve a package to its DLL path. Downloads if not cached.
    /// </summary>
    public static async Task<ResolvedPackage> ResolveAsync(
        string packageName,
        string? requestedVersion,
        string? requestedFramework,
        CancellationToken cancellationToken = default)
    {
#pragma warning disable CA1308 // NuGet API requires lowercase package names
        var packageId = packageName.ToLowerInvariant();
#pragma warning restore CA1308

        // 1. Find or download package
        var (packageDir, version) = await FindOrDownloadPackageAsync(
            packageId, packageName, requestedVersion, cancellationToken).ConfigureAwait(false);

        // 2. Select TFM
        var (framework, dllDir) = SelectFramework(packageDir, requestedFramework);

        // 3. Find primary DLL
        var dllPath = FindPrimaryDll(dllDir, packageId);

        // 4. Find XML doc file
        var xmlDocPath = Path.ChangeExtension(dllPath, ".xml");
        if (!File.Exists(xmlDocPath))
        {
            xmlDocPath = null;
        }

        return new ResolvedPackage(
            PackageId: packageName,
            Version: version,
            Framework: framework,
            DllPath: dllPath,
            XmlDocPath: xmlDocPath,
            PackageDir: packageDir);
    }

    /// <summary>
    /// Resolve a package to its directory and version only (no DLL needed).
    /// Use for commands like info/deps that only read the nuspec.
    /// </summary>
    public static async Task<ResolvedPackage> ResolveMetadataOnlyAsync(
        string packageName,
        string? requestedVersion,
        CancellationToken cancellationToken = default)
    {
#pragma warning disable CA1308 // NuGet API requires lowercase package names
        var packageId = packageName.ToLowerInvariant();
#pragma warning restore CA1308

        var (packageDir, version) = await FindOrDownloadPackageAsync(
            packageId, packageName, requestedVersion, cancellationToken).ConfigureAwait(false);

        return new ResolvedPackage(
            PackageId: packageName,
            Version: version,
            Framework: null,
            DllPath: null,
            XmlDocPath: null,
            PackageDir: packageDir);
    }

    private static async Task<(string PackageDir, string Version)> FindOrDownloadPackageAsync(
        string packageId,
        string originalName,
        string? requestedVersion,
        CancellationToken cancellationToken)
    {
        var packageCacheDir = Path.Combine(NuGetCacheDir, packageId);

        // Handle "latest", "latest-stable", "latest-prerelease" keywords
        if (requestedVersion is not null && IsVersionKeyword(requestedVersion))
        {
            requestedVersion = await ResolveVersionKeywordAsync(
                packageId, requestedVersion, cancellationToken).ConfigureAwait(false);
        }

        if (requestedVersion is not null)
        {
#pragma warning disable CA1308 // NuGet cache uses lowercase
            var versionDir = Path.Combine(packageCacheDir, requestedVersion.ToLowerInvariant());
#pragma warning restore CA1308
            if (Directory.Exists(versionDir))
            {
                return (versionDir, requestedVersion);
            }
        }
        else if (Directory.Exists(packageCacheDir))
        {
            // Pick highest stable version
            var version = GetHighestVersion(packageCacheDir);
            if (version is not null)
            {
                return (Path.Combine(packageCacheDir, version), version);
            }
        }

        // Need to resolve/download
        var resolvedVersion = requestedVersion
            ?? await ResolveLatestVersionAsync(packageId, cancellationToken).ConfigureAwait(false);

        // Check cache again with resolved version
#pragma warning disable CA1308 // NuGet cache uses lowercase
        var resolvedDir = Path.Combine(packageCacheDir, resolvedVersion.ToLowerInvariant());
#pragma warning restore CA1308
        if (Directory.Exists(resolvedDir))
        {
            return (resolvedDir, resolvedVersion);
        }

        // Download
        await DownloadPackageAsync(originalName, resolvedVersion, cancellationToken).ConfigureAwait(false);

#pragma warning disable CA1308 // NuGet cache uses lowercase
        resolvedDir = Path.Combine(packageCacheDir, resolvedVersion.ToLowerInvariant());
#pragma warning restore CA1308
        if (!Directory.Exists(resolvedDir))
        {
            throw new InvalidOperationException(
                $"Package '{originalName}' version '{resolvedVersion}' was not found after download. " +
                $"Expected at: {resolvedDir}");
        }

        return (resolvedDir, resolvedVersion);
    }

    private static string? GetHighestVersion(string packageCacheDir)
    {
        var versions = Directory.GetDirectories(packageCacheDir)
            .Select(d => Path.GetFileName(d))
            .Where(v => v is not null && !v.Contains('-', StringComparison.Ordinal)) // stable only
            .OrderByDescending(v => v, NuGetVersionComparer.Instance)
            .ToList();

        if (versions.Count == 0)
        {
            // Fall back to any version (including prerelease)
            versions = Directory.GetDirectories(packageCacheDir)
                .Select(d => Path.GetFileName(d))
                .Where(v => v is not null)
                .OrderByDescending(v => v, NuGetVersionComparer.Instance)
                .ToList();
        }

        return versions.FirstOrDefault();
    }

    private static async Task<string> ResolveLatestVersionAsync(
        string packageId,
        CancellationToken cancellationToken)
    {
        using var http = new HttpClient();
        var url = $"https://api.nuget.org/v3-flatcontainer/{packageId}/index.json";
        var response = await http.GetFromJsonAsync<NuGetVersionIndex>(url, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not resolve versions for package '{packageId}'.");

        // Pick latest stable, or latest prerelease if no stable
        var stable = response.Versions?
            .Where(v => !v.Contains('-', StringComparison.Ordinal))
            .LastOrDefault();

        var version = stable ?? response.Versions?.LastOrDefault()
            ?? throw new InvalidOperationException($"No versions found for package '{packageId}'.");

        return version;
    }

    private static async Task DownloadPackageAsync(
        string packageName,
        string version,
        CancellationToken cancellationToken)
    {
        // Use dotnet nuget to restore the package into the global cache
        var tmpDir = Path.Combine(Path.GetTempPath(), "nuget-docs-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tmpDir);

        try
        {
            // Create a temporary project that references the package
            var csproj = $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="{packageName}" Version="{version}" />
                  </ItemGroup>
                </Project>
                """;

            await File.WriteAllTextAsync(
                Path.Combine(tmpDir, "tmp.csproj"), csproj, cancellationToken).ConfigureAwait(false);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "restore",
                WorkingDirectory = tmpDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = System.Diagnostics.Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start 'dotnet restore'.");

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"Failed to download package '{packageName}@{version}': {stderr}");
            }
        }
        finally
        {
            try { Directory.Delete(tmpDir, recursive: true); } catch { /* best effort */ }
        }
    }

    private static (string Framework, string DllDir) SelectFramework(
        string packageDir,
        string? requestedFramework)
    {
        // Check both lib/ and ref/ directories
        var searchDirs = new[] { "lib", "ref" };
        var candidates = new List<(string Framework, string Dir)>();

        foreach (var subDir in searchDirs)
        {
            var baseDir = Path.Combine(packageDir, subDir);
            if (!Directory.Exists(baseDir))
            {
                continue;
            }

            foreach (var dir in Directory.GetDirectories(baseDir))
            {
                var tfm = Path.GetFileName(dir);
                if (tfm is not null && Directory.GetFiles(dir, "*.dll").Length > 0)
                {
                    candidates.Add((tfm, dir));
                }
            }
        }

        if (candidates.Count == 0)
        {
            // Check if this is a meta-package by reading the nuspec for dependencies
            var suggestion = GetMetaPackageSuggestion(packageDir);
            var message = $"No DLLs found in package at '{packageDir}'.";
            if (suggestion is not null)
            {
                message += $" This appears to be a meta-package. Try: {suggestion}";
            }
            else
            {
                message += " Check lib/ and ref/ directories.";
            }

            throw new InvalidOperationException(message);
        }

        if (requestedFramework is not null)
        {
            var match = candidates.FirstOrDefault(c =>
                string.Equals(c.Framework, requestedFramework, StringComparison.OrdinalIgnoreCase));

            if (match != default)
            {
                return match;
            }

            var available = string.Join(", ", candidates.Select(c => c.Framework).Distinct());
            throw new InvalidOperationException(
                $"Framework '{requestedFramework}' not found. Available: {available}");
        }

        // Auto-select by preference
        foreach (var preferred in TfmPreference)
        {
            var match = candidates.FirstOrDefault(c =>
                string.Equals(c.Framework, preferred, StringComparison.OrdinalIgnoreCase));

            if (match != default)
            {
                return match;
            }
        }

        // Fallback to first available
        return candidates[0];
    }

    private static string FindPrimaryDll(string dllDir, string packageId)
    {
        var dlls = Directory.GetFiles(dllDir, "*.dll");

        if (dlls.Length == 0)
        {
            throw new InvalidOperationException($"No DLLs found in '{dllDir}'.");
        }

        if (dlls.Length == 1)
        {
            return dlls[0];
        }

        // Match package name (case-insensitive)
        var primaryDll = dlls.FirstOrDefault(d =>
            string.Equals(
                Path.GetFileNameWithoutExtension(d),
                packageId,
                StringComparison.OrdinalIgnoreCase));

        // Try matching with dots replaced (e.g., Microsoft.Extensions.AI -> Microsoft.Extensions.AI.dll)
        primaryDll ??= dlls.FirstOrDefault(d =>
            Path.GetFileNameWithoutExtension(d)?
                .Equals(packageId.Replace("-", ".", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase) == true);

        return primaryDll ?? dlls[0];
    }

    internal static bool IsVersionKeyword(string version) =>
        string.Equals(version, "latest", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(version, "latest-stable", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(version, "latest-prerelease", StringComparison.OrdinalIgnoreCase);

    internal static async Task<string> ResolveVersionKeywordAsync(
        string packageId,
        string keyword,
        CancellationToken cancellationToken)
    {
        using var http = new HttpClient();
        var url = $"https://api.nuget.org/v3-flatcontainer/{packageId}/index.json";
        var response = await http.GetFromJsonAsync<NuGetVersionIndex>(url, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not resolve versions for package '{packageId}'.");

        var versions = response.Versions ?? [];

        if (string.Equals(keyword, "latest-prerelease", StringComparison.OrdinalIgnoreCase))
        {
            return versions.Where(v => v.Contains('-', StringComparison.Ordinal)).LastOrDefault()
                ?? throw new InvalidOperationException(
                    $"No prerelease versions found for package '{packageId}'.");
        }

        // "latest" and "latest-stable" both prefer stable, fall back to any
        var latestStable = versions.Where(v => !v.Contains('-', StringComparison.Ordinal)).LastOrDefault();

        if (string.Equals(keyword, "latest-stable", StringComparison.OrdinalIgnoreCase))
        {
            return latestStable
                ?? throw new InvalidOperationException(
                    $"No stable versions found for package '{packageId}'.");
        }

        // "latest" — prefer stable, fall back to prerelease
        return latestStable ?? versions.LastOrDefault()
            ?? throw new InvalidOperationException($"No versions found for package '{packageId}'.");
    }

    /// <summary>
    /// Reads the nuspec to suggest dependency packages when no DLLs are found (meta-package).
    /// </summary>
    private static string? GetMetaPackageSuggestion(string packageDir)
    {
        try
        {
            var nuspecFiles = Directory.GetFiles(packageDir, "*.nuspec");
            if (nuspecFiles.Length == 0) return null;

            var doc = System.Xml.Linq.XDocument.Load(nuspecFiles[0]);
            var ns = doc.Root?.GetDefaultNamespace() ?? System.Xml.Linq.XNamespace.None;
            var dependencies = doc.Root?.Element(ns + "metadata")?.Element(ns + "dependencies");
            if (dependencies is null) return null;

            var packageName = Path.GetFileName(packageDir);
            var depIds = dependencies.Descendants(ns + "dependency")
                .Select(d => d.Attribute("id")?.Value)
                .Where(id => id is not null)
                .Distinct()
                // Prioritize short names closest to the package name (e.g., Humanizer.Core before Humanizer.Core.af)
                .OrderBy(id => id!.Length)
                .ThenBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (depIds.Count == 0) return null;
            if (depIds.Count == 1) return depIds[0];

            // Detect common prefix pattern (e.g., Humanizer.Core.af, Humanizer.Core.ar → Humanizer.Core)
            // If all deps share a common dotted prefix longer than the package name, suggest the prefix
            var commonPrefix = GetCommonDottedPrefix(depIds!);
            if (commonPrefix is not null &&
                commonPrefix.Contains('.', StringComparison.Ordinal) &&
                !string.Equals(commonPrefix, depIds[0], StringComparison.OrdinalIgnoreCase))
            {
                return commonPrefix;
            }

            // Show up to 3 suggestions
            var shown = depIds.Take(3).ToList();
            var result = string.Join(", ", shown);
            if (depIds.Count > 3) result += $" (and {depIds.Count - 3} more)";
            return result;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Finds the longest common dotted prefix among all dependency IDs.
    /// E.g., ["Humanizer.Core.af", "Humanizer.Core.ar"] → "Humanizer.Core"
    /// Returns null if no meaningful common prefix exists.
    /// </summary>
    private static string? GetCommonDottedPrefix(List<string?> ids)
    {
        if (ids.Count == 0 || ids[0] is null) return null;

        var segments = ids[0]!.Split('.');
        var commonSegmentCount = segments.Length;

        for (var i = 1; i < ids.Count; i++)
        {
            if (ids[i] is null) return null;
            var otherSegments = ids[i]!.Split('.');
            var maxCommon = Math.Min(commonSegmentCount, otherSegments.Length);
            var matching = 0;
            for (var j = 0; j < maxCommon; j++)
            {
                if (string.Equals(segments[j], otherSegments[j], StringComparison.OrdinalIgnoreCase))
                {
                    matching++;
                }
                else
                {
                    break;
                }
            }
            commonSegmentCount = matching;
            if (commonSegmentCount == 0) return null;
        }

        // Need at least 2 segments for a meaningful prefix (e.g., "Package.Core")
        if (commonSegmentCount < 2) return null;

        return string.Join('.', segments.Take(commonSegmentCount));
    }

#pragma warning disable CA1812 // Instantiated via JSON deserialization
    private sealed class NuGetVersionIndex
#pragma warning restore CA1812
    {
        [JsonPropertyName("versions")]
        public List<string>? Versions { get; set; }
    }
}
