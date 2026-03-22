using System.Text;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace NugetDocs.Cli.Services;

/// <summary>
/// ILSpy-based type inspection and decompilation.
/// </summary>
internal sealed class TypeInspector : IDisposable
{
    private readonly CSharpDecompiler _decompiler;
    private readonly PEFile _peFile;

    public TypeInspector(string dllPath, string? xmlDocPath)
    {
        _peFile = new PEFile(dllPath);

        var settings = new DecompilerSettings(LanguageVersion.CSharp1)
        {
            ThrowOnAssemblyResolveErrors = false,
            AlwaysQualifyMemberReferences = false,
            ShowXmlDocumentation = xmlDocPath is not null,
        };

        // Let ILSpy auto-detect the best language version
        settings.SetLanguageVersion(LanguageVersion.Latest);

        _decompiler = new CSharpDecompiler(_peFile, new UniversalAssemblyResolver(
            dllPath, false, _peFile.DetectTargetFrameworkId()), settings);

        // If XML doc path exists alongside the DLL, ILSpy picks it up automatically
    }

    /// <summary>
    /// Get all public types, filtered to remove compiler-generated noise.
    /// </summary>
    public IReadOnlyList<TypeInfo> GetPublicTypes()
    {
        var types = new List<TypeInfo>();

        foreach (var type in _decompiler.TypeSystem.MainModule.TypeDefinitions)
        {
            if (!IsPublicApiType(type))
            {
                continue;
            }

            types.Add(new TypeInfo(
                FullName: type.FullName,
                Name: type.Name,
                Namespace: type.Namespace,
                Kind: GetTypeKind(type),
                GenericParameterCount: type.TypeParameterCount));
        }

        return types.OrderBy(t => t.Namespace).ThenBy(t => t.Name).ToList();
    }

    /// <summary>
    /// Decompile a specific type to C# source with XML doc comments.
    /// </summary>
    public string DecompileType(string typeName)
    {
        var fullName = ResolveTypeName(typeName);
        var result = _decompiler.DecompileTypeAsString(new FullTypeName(fullName));
        return result;
    }

    /// <summary>
    /// Search types and members by pattern (glob-like: * matches any).
    /// </summary>
    public IReadOnlyList<SearchResult> SearchTypes(string pattern)
    {
        var results = new List<SearchResult>();
        var regex = GlobToRegex(pattern);

        foreach (var type in _decompiler.TypeSystem.MainModule.TypeDefinitions)
        {
            if (!IsPublicApiType(type))
            {
                continue;
            }

            // Match type name
            if (regex.IsMatch(type.Name) || regex.IsMatch(type.FullName))
            {
                results.Add(new SearchResult(
                    Kind: GetTypeKind(type),
                    FullName: type.FullName,
                    Name: type.Name,
                    MemberKind: null));
            }

            // Search members
            foreach (var member in type.Members)
            {
                if (member.Accessibility != Accessibility.Public)
                {
                    continue;
                }

                if (regex.IsMatch(member.Name))
                {
                    results.Add(new SearchResult(
                        Kind: GetTypeKind(type),
                        FullName: $"{type.FullName}.{member.Name}",
                        Name: member.Name,
                        MemberKind: GetMemberKind(member)));
                }
            }
        }

        return results.OrderBy(r => r.FullName).ToList();
    }

    /// <summary>
    /// Resolve a short type name to its full name.
    /// </summary>
    public string ResolveTypeName(string typeName)
    {
        // If it looks like a full name already
        if (typeName.Contains('.'))
        {
            return typeName;
        }

        var matches = _decompiler.TypeSystem.MainModule.TypeDefinitions
            .Where(t => IsPublicApiType(t) &&
                string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count switch
        {
            0 => throw new InvalidOperationException(
                $"Type '{typeName}' not found in this package."),
            1 => matches[0].FullName,
            _ => throw new InvalidOperationException(
                $"Ambiguous type name '{typeName}'. Candidates:\n" +
                string.Join("\n", matches.Select(m => $"  {m.FullName}"))),
        };
    }

    private static bool IsPublicApiType(ITypeDefinition type)
    {
        // Must be public
        if (type.Accessibility != Accessibility.Public &&
            type.Accessibility != Accessibility.Protected)
        {
            return false;
        }

        var name = type.Name;
        var fullName = type.FullName;

        // Filter compiler-generated
        if (name.Contains('<') || name.Contains('>') ||
            name.StartsWith("__", StringComparison.Ordinal) ||
            name == "<Module>" ||
            name == "<PrivateImplementationDetails>")
        {
            return false;
        }

        // Filter internal infrastructure namespaces
        if (fullName.StartsWith("Microsoft.Shared.", StringComparison.Ordinal) ||
            fullName.StartsWith("System.Text.RegularExpressions.Generated.", StringComparison.Ordinal) ||
            fullName.Contains(".DisplayClass") ||
            fullName.Contains(".DebugView"))
        {
            return false;
        }

        // Filter nested compiler-generated types
        if (type.DeclaringType is not null &&
            !IsPublicApiType(type.DeclaringType.GetDefinition()!))
        {
            return false;
        }

        return true;
    }

    private static string GetTypeKind(ITypeDefinition type)
    {
        if (type.Kind == TypeKind.Interface)
        {
            return "Interface";
        }

        if (type.Kind == TypeKind.Enum)
        {
            return "Enum";
        }

        if (type.Kind == TypeKind.Delegate)
        {
            return "Delegate";
        }

        if (type.Kind == TypeKind.Struct)
        {
            return "Struct";
        }

        return "Class";
    }

    private static string? GetMemberKind(IMember member)
    {
        return member switch
        {
            IMethod m when m.IsConstructor => "Constructor",
            IMethod => "Method",
            IProperty => "Property",
            IField => "Field",
            IEvent => "Event",
            _ => null,
        };
    }

    private static System.Text.RegularExpressions.Regex GlobToRegex(string pattern)
    {
        var regexPattern = "^" +
            System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") +
            "$";

        return new System.Text.RegularExpressions.Regex(
            regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    public void Dispose()
    {
        _peFile.Dispose();
    }

    public record TypeInfo(
        string FullName,
        string Name,
        string Namespace,
        string Kind,
        int GenericParameterCount);

    public record SearchResult(
        string Kind,
        string FullName,
        string Name,
        string? MemberKind);
}
