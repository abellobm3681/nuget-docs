using System.CommandLine;
using System.CommandLine.Invocation;
using System.Xml.Linq;
using NugetDocs.Cli.Services;

namespace NugetDocs.Cli.Commands;

internal sealed class InfoCommandAction(InfoCommand command) : AsynchronousCommandLineAction
{
    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        var package = parseResult.GetValue(command.PackageArgument)!;
        var version = parseResult.GetValue(command.VersionOption);

        try
        {
            var resolved = await PackageResolver.ResolveAsync(
                package, version, requestedFramework: null, cancellationToken).ConfigureAwait(false);

            // Find .nuspec file
            var nuspecFiles = Directory.GetFiles(resolved.PackageDir, "*.nuspec");
            if (nuspecFiles.Length == 0)
            {
                Console.Error.WriteLine("No .nuspec file found in package.");
                return 1;
            }

            var doc = XDocument.Load(nuspecFiles[0]);
            var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
            var metadata = doc.Root?.Element(ns + "metadata");

            if (metadata is null)
            {
                Console.Error.WriteLine("Invalid .nuspec: no metadata element.");
                return 1;
            }

            Console.WriteLine($"Package: {GetValue(metadata, ns, "id")}");
            Console.WriteLine($"Version: {GetValue(metadata, ns, "version")}");
            Console.WriteLine($"Authors: {GetValue(metadata, ns, "authors")}");
            Console.WriteLine($"Description: {GetValue(metadata, ns, "description")}");

            var license = GetValue(metadata, ns, "license");
            var licenseUrl = GetValue(metadata, ns, "licenseUrl");
            if (license is not null)
            {
                Console.WriteLine($"License: {license}");
            }
            else if (licenseUrl is not null)
            {
                Console.WriteLine($"License URL: {licenseUrl}");
            }

            var projectUrl = GetValue(metadata, ns, "projectUrl");
            if (projectUrl is not null)
            {
                Console.WriteLine($"Project URL: {projectUrl}");
            }

            var tags = GetValue(metadata, ns, "tags");
            if (tags is not null)
            {
                Console.WriteLine($"Tags: {tags}");
            }

            // List target frameworks
            var libDir = Path.Combine(resolved.PackageDir, "lib");
            if (Directory.Exists(libDir))
            {
                var tfms = Directory.GetDirectories(libDir)
                    .Select(Path.GetFileName)
                    .Where(n => n is not null)
                    .ToList();

                if (tfms.Count > 0)
                {
                    Console.WriteLine($"Frameworks: {string.Join(", ", tfms)}");
                }
            }

            // Dependencies
            var dependencies = metadata.Element(ns + "dependencies");
            if (dependencies is not null)
            {
                Console.WriteLine();
                Console.WriteLine("Dependencies:");

                var groups = dependencies.Elements(ns + "group").ToList();
                if (groups.Count > 0)
                {
                    foreach (var group in groups)
                    {
                        var tfm = group.Attribute("targetFramework")?.Value ?? "any";
                        var deps = group.Elements(ns + "dependency").ToList();
                        if (deps.Count > 0)
                        {
                            Console.WriteLine($"  {tfm}:");
                            foreach (var dep in deps)
                            {
                                var depId = dep.Attribute("id")?.Value;
                                var depVer = dep.Attribute("version")?.Value;
                                Console.WriteLine($"    {depId} {depVer}");
                            }
                        }
                    }
                }
                else
                {
                    // Flat dependency list
                    foreach (var dep in dependencies.Elements(ns + "dependency"))
                    {
                        var depId = dep.Attribute("id")?.Value;
                        var depVer = dep.Attribute("version")?.Value;
                        Console.WriteLine($"  {depId} {depVer}");
                    }
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

    private static string? GetValue(XElement parent, XNamespace ns, string name)
    {
        var value = parent.Element(ns + name)?.Value?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
