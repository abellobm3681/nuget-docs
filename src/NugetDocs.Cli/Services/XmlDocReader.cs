using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NugetDocs.Cli.Services;

/// <summary>
/// Parses XML documentation files and provides member summaries.
/// </summary>
internal sealed partial class XmlDocReader
{
    public record XmlDocEntry(string Summary, string? Returns, string? Remarks);

    private readonly Dictionary<string, XmlDocEntry> _entries = new(StringComparer.Ordinal);

    public static XmlDocReader? TryLoad(string? xmlDocPath)
    {
        if (xmlDocPath is null || !File.Exists(xmlDocPath))
        {
            return null;
        }

        try
        {
            var reader = new XmlDocReader();
            reader.Load(xmlDocPath);
            return reader;
        }
        catch
        {
            return null;
        }
    }

    private void Load(string xmlDocPath)
    {
        var doc = XDocument.Load(xmlDocPath);
        var members = doc.Root?.Element("members")?.Elements("member");

        if (members is null)
        {
            return;
        }

        foreach (var member in members)
        {
            var name = member.Attribute("name")?.Value;
            if (name is null)
            {
                continue;
            }

            var summary = CleanXmlText(member.Element("summary")?.Value);
            var returns = CleanXmlText(member.Element("returns")?.Value);
            var remarks = CleanXmlText(member.Element("remarks")?.Value);

            if (summary is not null)
            {
                _entries[name] = new XmlDocEntry(summary, returns, remarks);
            }
        }
    }

    /// <summary>
    /// Get summary for a type by its full name (e.g., "Microsoft.Extensions.AI.IChatClient").
    /// </summary>
    public string? GetTypeSummary(string fullTypeName)
    {
        var key = $"T:{fullTypeName}";
        return _entries.TryGetValue(key, out var entry) ? entry.Summary : null;
    }

    /// <summary>
    /// Get all entries (for search).
    /// </summary>
    public IReadOnlyDictionary<string, XmlDocEntry> Entries => _entries;

    private static string? CleanXmlText(string? text)
    {
        if (text is null)
        {
            return null;
        }

        // Remove XML tags like <see cref="..."/>, <paramref name="..."/>, etc.
        var cleaned = XmlTagRegex().Replace(text, "$1");
        // Collapse whitespace
        cleaned = WhitespaceRegex().Replace(cleaned, " ").Trim();

        return cleaned.Length == 0 ? null : cleaned;
    }

    [GeneratedRegex(@"<see\s+cref=""[^""]*?\.?([^"".]+)""\s*/>")]
    private static partial Regex XmlTagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
