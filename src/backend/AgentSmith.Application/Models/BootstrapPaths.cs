using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0496: the two files the bootstrap probe reads for one context, composed once so the
/// probe and the refusal that quotes it can never name different paths.
/// <para>
/// 2026-09-01-eec0: <see cref="RetiredPrinciples"/> is not a third bootstrap file. It is
/// the pre-rename name, carried here only so a repository still holding it is refused with
/// re-init named as the remedy.
/// </para>
/// <para>
/// 2026-09-23-4711: <see cref="MetaDir"/> and <see cref="RetiredMetaDir"/> are the same two
/// answers one level up — the directory the files live in, and where that directory is moved
/// when the derivation stops producing the context. Composed here for the same reason the
/// files are: the move and the probe must never disagree about where a context sits.
/// </para>
/// </summary>
public sealed record BootstrapPaths(
    string ContextYaml, string Principles, string RetiredPrinciples,
    string MetaDir, string RetiredMetaDir)
{
    /// <summary>2026-11-25-9992c: under <paramref name="root"/>, the repository root of the sandbox they are read in.</summary>
    public static BootstrapPaths For(string root, string contextName)
    {
        var metaDir = ProjectMetaPaths.MetaDirFor(root, contextName);
        return new BootstrapPaths(
            $"{metaDir}/{ProjectMetaPaths.ContextYamlFile}",
            $"{metaDir}/{ProjectMetaPaths.PrinciplesFile}",
            $"{metaDir}/{ProjectMetaPaths.RetiredPrinciplesFile}",
            metaDir,
            ProjectMetaPaths.RetiredMetaDirFor(root, contextName));
    }

    public IReadOnlyList<string> All => [ContextYaml, Principles];
}
