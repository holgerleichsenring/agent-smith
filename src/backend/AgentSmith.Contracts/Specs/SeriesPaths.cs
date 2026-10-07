namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-10-06-03c7d: where a series lies. Its specs and their same-stem companions sit in the
/// shared state directories under <see cref="SpecsRoot"/> — a spec's state IS its directory — and
/// one manifest per series sits at <c>series/{base}.yaml</c>. Every file of a series starts with
/// its base, so a ticket's files coexist with every other ticket's after a merge.
/// </summary>
public static class SeriesPaths
{
    /// <summary>Repo-relative root of the spec state directories.</summary>
    public const string SpecsRoot = ".agentsmith/specs";

    /// <summary>Repo-relative directory holding one manifest per series.</summary>
    public const string SeriesRoot = ".agentsmith/series";

    /// <summary>Specs no run has executed yet.</summary>
    public const string Planned = SpecsRoot + "/planned";

    /// <summary>Specs a run executed.</summary>
    public const string Done = SpecsRoot + "/done";

    /// <summary>Specs an operator is working by hand; the product never writes here.</summary>
    public const string Active = SpecsRoot + "/active";

    private const string SpecExtension = ".yaml";
    private const string CompanionExtension = ".md";

    /// <summary>The manifest of the series with base <paramref name="seriesBase"/>.</summary>
    public static string Manifest(string seriesBase) => $"{SeriesRoot}/{seriesBase}{SpecExtension}";

    /// <summary>A spec file in <paramref name="directory"/>.</summary>
    public static string Spec(string directory, string stem) => $"{directory}/{stem}{SpecExtension}";

    /// <summary>A spec's markdown companion in <paramref name="directory"/>.</summary>
    public static string Companion(string directory, string stem) => $"{directory}/{stem}{CompanionExtension}";

    /// <summary>A git pathspec matching every spec file of the series in any state directory.</summary>
    public static string SpecFilesPathspec(string seriesBase) => $"{SpecsRoot}/*/{seriesBase}*";

    /// <summary>The last path segment of a listed entry, whatever form the lister used.</summary>
    public static string FileName(string path) => path.TrimEnd('/').Split('/', '\\')[^1];

    /// <summary>True when <paramref name="fileName"/> is a spec file (yaml).</summary>
    public static bool IsSpecFile(string fileName) =>
        fileName.EndsWith(SpecExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>The stem of a spec or companion file name.</summary>
    public static string StemOf(string fileName) => Path.GetFileNameWithoutExtension(fileName);

    /// <summary>
    /// True when <paramref name="fileName"/> belongs to <paramref name="id"/>: it starts with the
    /// id and the next character is '-' (a label) or '.' (an extension). A longer id never matches.
    /// </summary>
    public static bool BelongsTo(string fileName, string id) =>
        fileName.Length > id.Length
        && fileName.StartsWith(id, StringComparison.Ordinal)
        && fileName[id.Length] is '-' or '.';

    /// <summary>True when <paramref name="fileName"/> belongs to a member of the series: the base
    /// followed by a member's lowercase letter.</summary>
    public static bool BelongsToSeries(string fileName, string seriesBase) =>
        fileName.Length > seriesBase.Length
        && fileName.StartsWith(seriesBase, StringComparison.Ordinal)
        && char.IsAsciiLetterLower(fileName[seriesBase.Length]);
}
