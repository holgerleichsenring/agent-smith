using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AgentSmith.Application.Services.Validation;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0521: one phase spec on disk, read as the two things a rule judges — the slug that
/// became its file name, and the document inside it.
/// <para>
/// The namespace is read off the id, never off an ordering. Since the counter namespace
/// closed the two id shapes are ALTERNATIVES, not one sequence: compared as text a
/// date-minted id sorts BELOW every counter id, so a rule scoped "at or above this id"
/// would exempt every phase minted from now on and still ship green.
/// </para>
/// <para>
/// A stem the reader cannot split is kept, not dropped — an unreadable name is the one
/// case where dropping it would make every rule below silently stop judging it.
/// </para>
/// </summary>
internal sealed partial class PhaseSpecFile
{
    [GeneratedRegex(
        @"^(?<id>\d{4}-\d{2}-\d{2}-[0-9a-f]{4}|p\d{4,6}[a-z]?(?:-pre)?)(?:-(?<slug>.*))?$")]
    private static partial Regex FileStemRegex();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}-[0-9a-f]{4}$")]
    private static partial Regex DateMintedRegex();

    private readonly Lazy<JsonNode?> _document;

    private PhaseSpecFile(string path, string phaseId, string slug)
    {
        Path = path;
        PhaseId = phaseId;
        Slug = slug;
        IsDateMinted = DateMintedRegex().IsMatch(phaseId);
        _document = new Lazy<JsonNode?>(() => Parse(path));
    }

    public string Path { get; }

    /// <summary>The id the file name claims — the fixed-width prefix, not the whole stem.</summary>
    public string PhaseId { get; }

    /// <summary>The descriptive tail of the file name, which is what a reader sees first.</summary>
    public string Slug { get; }

    /// <summary>True for the open namespace. The closed counter namespace is not judged.</summary>
    public bool IsDateMinted { get; }

    public int SlugWords => Slug.Split('-', StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>Null when the file is not parseable YAML at all.</summary>
    public JsonNode? Document => _document.Value;

    public int GoalLength => (Document?["goal"] as JsonValue)?.GetValue<string>()?.Length ?? 0;

    public static IReadOnlyList<PhaseSpecFile> All() =>
    [
        .. Directory
            .EnumerateFiles(
                System.IO.Path.Combine(ArchitectureSources.AgentSmithRoot, "phases"),
                "*.yaml", SearchOption.AllDirectories)
            .Select(FromPath)
            .OrderBy(file => file.Path, StringComparer.Ordinal),
    ];

    private static PhaseSpecFile FromPath(string path)
    {
        var stem = System.IO.Path.GetFileNameWithoutExtension(path);
        var match = FileStemRegex().Match(stem);
        return match.Success
            ? new PhaseSpecFile(path, match.Groups["id"].Value, match.Groups["slug"].Value)
            : new PhaseSpecFile(path, stem, string.Empty);
    }

    private static JsonNode? Parse(string path)
    {
        try
        {
            return YamlAsJson.Convert(File.ReadAllText(path));
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return null;
        }
    }
}
