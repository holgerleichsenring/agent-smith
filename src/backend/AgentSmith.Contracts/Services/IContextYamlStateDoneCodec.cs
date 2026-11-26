using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-08-26-31e5: the <c>state.done</c> block of a context.yaml. 2026-11-26-462aa: it is only ever READ OUT — the
/// done index lives in the context's done.md, and a context.yaml that still carries the block has its entries moved
/// there by the record step's first write.
/// <para>
/// The edit is a SPLICE, not a round trip: every other line of the file — the yaml-language-server header, the
/// operator's comments, the flow style they chose — is left exactly as it was.
/// </para>
/// </summary>
public interface IContextYamlStateDoneCodec
{
    /// <summary>
    /// The block's entries in file order and the file without the block (a <c>state:</c> left with nothing under it
    /// goes too); <see cref="ContextYamlDoneExtraction.None"/> when there is no <c>state.done</c>. A file that cannot
    /// be edited safely is reported rather than mangled.
    /// </summary>
    ContextYamlDoneExtraction Extract(string yaml);
}
