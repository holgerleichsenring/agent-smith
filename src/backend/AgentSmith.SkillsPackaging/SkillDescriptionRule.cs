namespace AgentSmith.Skills;

/// <summary>
/// p0518: THE declaration of what a master SKILL.md description may be. Over
/// <see cref="MaxChars"/> the skill loader silently drops the master and the run
/// dies later with "Prompt resource not found" (the v3.16.0 incident), so the
/// number is a live-failure boundary rather than a style preference.
/// <para>
/// This file is compiled into BOTH gates: the packaging gate over the release
/// tarball (AgentSmith.SkillsPackaging) and the parse-time gate on every catalog
/// load (AgentSmith.Infrastructure.Core), which links it as a shared source file
/// because the packaging tool builds BEFORE the runtime projects and cannot
/// reference them. One literal, one verdict, two gates.
/// </para>
/// </summary>
internal static class SkillDescriptionRule
{
    /// <summary>The loader's hard limit. The catalog's own gate declares the same number.</summary>
    public const int MaxChars = 200;

    /// <summary>Returns why <paramref name="description"/> is unacceptable, or null when it is fine.</summary>
    public static string? Violation(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return "description is missing or empty";
        // A single-line scalar is the only shape whose length is the same to a
        // YAML deserializer and to the shell gate that reads the file as text.
        // A block scalar keeps its newlines through deserialization, so this
        // catches the shape rather than guessing at the author's intent.
        if (description.Contains('\n'))
            return "description must be a single-line scalar; a YAML block scalar hides its length from the catalog gate";
        return description.Length > MaxChars
            ? $"description must be at most {MaxChars} chars; got {description.Length}"
            : null;
    }
}
