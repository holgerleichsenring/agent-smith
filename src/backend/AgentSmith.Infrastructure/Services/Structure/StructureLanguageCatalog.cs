using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Services.Structure;

/// <summary>
/// 2026-10-06-8565b: the languages the structure engine knows, read once from the embedded
/// data files. A path is claimed by extension; its exclude globs then decide whether it is
/// measured at all.
/// </summary>
public sealed class StructureLanguageCatalog
{
    private const string ResourcePrefix = "AgentSmith.Structure.";
    private readonly IReadOnlyList<(StructureLanguage Language, StructurePathFilter Filter)>
        _languages;

    public StructureLanguageCatalog() : this(Load()) { }

    internal StructureLanguageCatalog(IEnumerable<StructureLanguage> languages) =>
        _languages = [.. languages.Select(l => (l, new StructurePathFilter(l.Exclude)))];

    /// <summary>The language claiming <paramref name="path"/>, and whether it excludes it.</summary>
    public (StructureLanguage? Language, bool Excluded) For(string path)
    {
        var extension = Path.GetExtension(path);
        foreach (var (language, filter) in _languages)
            if (language.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                return (language, filter.Excludes(path));
        return (null, false);
    }

    private static IEnumerable<StructureLanguage> Load()
    {
        var assembly = typeof(StructureLanguageCatalog).Assembly;
        var yaml = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance).Build();
        var names = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal));
        foreach (var name in names)
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
            yield return yaml.Deserialize<StructureLanguage>(reader);
        }
    }
}
