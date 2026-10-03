using AgentSmith.Contracts.Models;
using YamlDotNet.Serialization;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// 2026-10-03-cf20c: reads one framework overlay file (principles/frameworks/&lt;slug&gt;.md,
/// format in the catalog's OVERLAY-FORMAT.md) — its Detection signals, and the Rules it renders
/// for one language. Every reader answers null on a malformed file: an overlay that cannot be
/// read whole applies nothing, because a wrongly applied overlay is a wrong mandate.
/// </summary>
internal static class FrameworkOverlaySection
{
    private const string Fence = "```";
    private const string AllLanguages = "All languages";

    /// <summary>The overlay's signals, or null when Detection is missing or malformed.</summary>
    public static FrameworkOverlay? Detection(string slug, string text)
    {
        var body = Section(Lines(text), "## Detection", "## ");
        var start = body.FindIndex(l => l.Trim() == Fence + "yaml");
        if (start < 0) return null;
        var end = body.FindIndex(start + 1, l => l.Trim() == Fence);
        if (end < 0) return null;
        var entries = new DeserializerBuilder().Build().Deserialize<List<Dictionary<string, string?>>?>(
            string.Join('\n', body.Skip(start + 1).Take(end - start - 1)));
        if (entries is null || entries.Count == 0) return null;

        var signals = new List<FrameworkOverlaySignal>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry is null || !entry.TryGetValue("file", out var file) || !IsRelative(file))
                return null;
            entry.TryGetValue("contains", out var contains);
            signals.Add(new FrameworkOverlaySignal(file!,
                string.IsNullOrEmpty(contains) ? null : contains));
        }
        return new FrameworkOverlay(slug, signals);
    }

    /// <summary>
    /// The overlay as composed for <paramref name="languageSlug"/>: its All-languages rules and
    /// the rules of that language's section when it has one. The overlay's own title is not
    /// rendered — the composer names the overlay by slug. Null without an All-languages section.
    /// </summary>
    public static string? Render(string slug, string text, string languageSlug)
    {
        var rules = Section(Lines(text), "## Rules", "## ");
        var all = Section(rules, "### " + AllLanguages, "### ");
        if (all.Count == 0) return null;
        var language = Section(rules, "### " + languageSlug, "### ");

        var rendered = $"# Framework Overlay: {slug}\n\n## {AllLanguages}\n\n{Join(all)}";
        return language.Count == 0
            ? rendered
            : $"{rendered}\n\n## {languageSlug}\n\n{Join(language)}";
    }

    // A signal file is relative to the component root, never rooted and never above it.
    private static bool IsRelative(string? file) =>
        !string.IsNullOrWhiteSpace(file)
        && !file.StartsWith('/') && !file.StartsWith('\\') && !Path.IsPathRooted(file)
        && !file.Split('/', '\\').Contains("..");

    private static List<string> Lines(string text) =>
        text.Replace("\r\n", "\n").Split('\n').ToList();

    // The lines under the heading equal to `heading`, up to the next heading at its level or
    // above; empty when the heading is absent. A fenced block is skipped whole, so a '#' line
    // inside an example is never read as a heading.
    private static List<string> Section(List<string> lines, string heading, string sameLevel)
    {
        var start = lines.FindIndex(l => string.Equals(l.TrimEnd(), heading, StringComparison.OrdinalIgnoreCase));
        if (start < 0) return [];
        var body = new List<string>();
        var fenced = false;
        foreach (var line in lines.Skip(start + 1))
        {
            if (line.TrimStart().StartsWith(Fence, StringComparison.Ordinal)) fenced = !fenced;
            else if (!fenced && (line.StartsWith(sameLevel, StringComparison.Ordinal) || IsHigher(line, sameLevel)))
                break;
            body.Add(line);
        }
        return body;
    }

    private static bool IsHigher(string line, string sameLevel) =>
        Enumerable.Range(1, sameLevel.Length - 2)
            .Any(level => line.StartsWith(new string('#', level) + " ", StringComparison.Ordinal));

    private static string Join(List<string> lines) => string.Join('\n', lines).Trim();
}
