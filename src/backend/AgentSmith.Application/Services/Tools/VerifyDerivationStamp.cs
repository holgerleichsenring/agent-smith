using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-09-01-e14d: records what a written verify block was derived from, and hashes it.
/// <para>
/// The model names the FILES — its own CI definition, its scripts, its manifests — because
/// only a reader of the repository knows which files state how it is verified. The
/// framework computes the HASH, because a model cannot compute one and a hash it invented
/// would report drift on the very next run.
/// </para>
/// <para>
/// A derivation with no stages derived from it is dropped: the record exists to make the
/// stages auditable, and a source pointer attached to nothing is a claim about work that
/// was never done.
/// </para>
/// </summary>
public sealed class VerifyDerivationStamp(VerifyDerivationDigest digest)
{
    public async Task<ContextYamlDocument> StampAsync(
        ContextYamlDocument document, ISandbox sandbox, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.VerifyDerivedFrom is not { Files.Count: > 0 } derivation)
            return document;
        if (document.Verify is not { Count: > 0 })
            return document with { VerifyDerivedFrom = null };

        var hash = await digest.ComputeAsync(sandbox, derivation.Files, ct);
        return document with { VerifyDerivedFrom = derivation with { Hash = hash } };
    }
}
