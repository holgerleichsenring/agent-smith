using System.Formats.Tar;
using System.IO.Compression;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

namespace AgentSmith.SkillsPackaging;

/// <summary>
/// p0325: validates master SKILL.md descriptions inside a skills release
/// tarball at packaging time. Mirrors the runtime rule in
/// <c>NewFormatSkillValidator</c> (AgentSmith.Infrastructure.Core): a master
/// description over the loader limit is silently dropped by pre-p0324 loaders
/// (the v3.16.0 incident) — here it becomes a build failure instead.
/// </summary>
public sealed partial class MasterDescriptionValidator
{
    /// <summary>
    /// Must match <c>NewFormatSkillValidator.MaxDescriptionChars</c>. Not
    /// referenced directly: this tool builds BEFORE the runtime projects, so
    /// depending on AgentSmith.Infrastructure.Core would be a build cycle.
    /// </summary>
    public const int MaxDescriptionChars = 200;

    [GeneratedRegex(@"(^|/)skills/_masters/(?<master>[^/]+)/SKILL\.md$")]
    private static partial Regex MasterSkillPath();

    private static readonly IDeserializer Frontmatter = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    public IReadOnlyList<MasterDescriptionViolation> Validate(Stream tarGzStream)
    {
        var violations = new List<MasterDescriptionViolation>();
        var masters = 0;
        using var gz = new GZipStream(tarGzStream, CompressionMode.Decompress, leaveOpen: true);
        using var tar = new TarReader(gz);
        while (tar.GetNextEntry() is { } entry)
        {
            var match = MasterSkillPath().Match(entry.Name);
            if (!match.Success || entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                continue;
            masters++;
            ValidateMaster(match.Groups["master"].Value, ReadEntry(entry), violations);
        }

        if (masters == 0)
            violations.Add(new MasterDescriptionViolation(
                "(tarball)", "no skills/_masters/*/SKILL.md entries found — not a skills release tarball?"));
        return violations;
    }

    private static void ValidateMaster(string master, string content, List<MasterDescriptionViolation> violations)
    {
        var description = ReadDescription(master, content, violations);
        if (description is null)
            return;
        if (string.IsNullOrWhiteSpace(description))
            violations.Add(new MasterDescriptionViolation(master, "description is missing or empty"));
        else if (description.Length > MaxDescriptionChars)
            violations.Add(new MasterDescriptionViolation(
                master,
                $"description is {description.Length} chars; the loader limit is {MaxDescriptionChars} — " +
                "over-limit masters are silently dropped by pre-p0324 loaders (v3.16.0 incident)"));
    }

    private static string? ReadDescription(string master, string content, List<MasterDescriptionViolation> violations)
    {
        var frontmatter = ExtractFrontmatter(content);
        if (frontmatter is null)
        {
            violations.Add(new MasterDescriptionViolation(master, "SKILL.md has no YAML frontmatter"));
            return null;
        }
        var fields = Frontmatter.Deserialize<Dictionary<string, object?>>(frontmatter);
        return fields.TryGetValue("description", out var value) ? value?.ToString() ?? string.Empty : string.Empty;
    }

    private static string? ExtractFrontmatter(string content)
    {
        var match = Regex.Match(content, @"\A---\s*\n(?<yaml>.*?)\n---\s*(\n|\z)", RegexOptions.Singleline);
        return match.Success ? match.Groups["yaml"].Value : null;
    }

    private static string ReadEntry(TarEntry entry)
    {
        if (entry.DataStream is null) return string.Empty;
        using var reader = new StreamReader(entry.DataStream, leaveOpen: true);
        return reader.ReadToEnd();
    }
}
