using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-08-26-31e5: upserts one phase index line into a context.yaml's <c>state.done</c>.
/// <para>
/// The edit is a SPLICE, not a round trip: <c>state.done</c> is the chronicle, and every
/// other line of the file — the yaml-language-server header, the operator's comments, the
/// flow style they chose — is left exactly as it was. A phase-record step runs on every
/// finished phase, so a round trip would strip a target repository's comments on the first
/// run and never give them back.
/// </para>
/// </summary>
public interface IContextYamlStateDoneCodec
{
    /// <summary>
    /// Replaces the entry for <paramref name="phaseId"/> or adds it FIRST — newest first,
    /// the order this repository already writes. A file that cannot be edited safely is
    /// reported rather than mangled.
    /// </summary>
    ContextYamlUpsertResult Upsert(string? yaml, string phaseId, string entry);
}
