namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-09-01-e14d: where a repository's <c>verify:</c> block came from — the files in
/// that repository which already state how it is built and tested — and a content hash
/// of them taken when the block was written.
/// <para>
/// In an established estate the truth about "green" is already written down: an Azure
/// Pipelines definition, a workflow file, a Makefile, a tool's own help output. A
/// declaration derived from those is an ADOPTION of the gate the estate really runs;
/// one written from scratch is an invention that can only disagree with it. Recording
/// the source is what makes "derive once" checkable instead of merely cheap — a
/// derivation whose source nobody named is a guess nobody can audit.
/// </para>
/// <para>
/// WHAT THE HASH CANNOT SEE, said out loud: it sees the pipeline file move, not the
/// TARGET move. A cluster id, a schema name, a service connection can all change while
/// every byte here stays identical, and the only thing that finds that out is the stage
/// itself, when it runs and goes red.
/// </para>
/// </summary>
/// <param name="Files">Paths, relative to the declaring context's workdir, that the
/// stages were derived from. Named by whoever derived them; never inferred here.</param>
/// <param name="Hash">The framework's digest of those files' content at derivation time.
/// Written by the write path, not by the model — a model cannot compute one, and a hash
/// it invented would report drift forever. Null until a write path stamps it.</param>
public sealed record ContextYamlVerifyDerivation(
    IReadOnlyList<string> Files, string? Hash = null);
