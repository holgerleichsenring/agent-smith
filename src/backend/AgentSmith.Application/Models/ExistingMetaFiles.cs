namespace AgentSmith.Application.Models;

/// <summary>
/// What a bootstrap round finds already written for one context.
/// <para>
/// 2026-09-01-72c5: <see cref="RetiredRenamed"/> says the round had to move a
/// pre-rename principles file to the current name before it could look, and
/// <see cref="Error"/> says that move failed — the round then stops rather than
/// composing over ratified content it could not migrate.
/// </para>
/// </summary>
public sealed record ExistingMetaFiles(
    string? ContextYaml, string? Principles, bool RetiredRenamed, string? Error = null);
