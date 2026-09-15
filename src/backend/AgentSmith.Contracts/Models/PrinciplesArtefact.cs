namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-09-15-d66f: one enforcement file a language delta declares — the artefact that makes
/// one of its rules CHECKABLE instead of merely stated.
/// <para>
/// <paramref name="Path"/> is REPOSITORY-ROOT relative, because an artefact belongs where the
/// stack's build already looks; beside the composed principles it would be read by nothing.
/// <paramref name="Content"/> names a mechanism of the language and no type, file or namespace
/// of any target — which is what lets two repositories of one stack receive the same bytes.
/// </para>
/// </summary>
public sealed record PrinciplesArtefact(string Path, string Content);
