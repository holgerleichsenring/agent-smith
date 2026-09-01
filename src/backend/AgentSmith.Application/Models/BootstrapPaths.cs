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
/// </summary>
public sealed record BootstrapPaths(
    string ContextYaml, string Principles, string RetiredPrinciples)
{
    public static BootstrapPaths For(string contextName)
    {
        var metaDir = ProjectMetaPaths.MetaDirFor(contextName);
        return new BootstrapPaths(
            $"{metaDir}/{ProjectMetaPaths.ContextYamlFile}",
            $"{metaDir}/{ProjectMetaPaths.PrinciplesFile}",
            $"{metaDir}/{ProjectMetaPaths.RetiredPrinciplesFile}");
    }

    public IReadOnlyList<string> All => [ContextYaml, Principles];
}
