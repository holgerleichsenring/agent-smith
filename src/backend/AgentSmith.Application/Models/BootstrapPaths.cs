using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0496: the two files the bootstrap probe reads for one context, composed once so the
/// probe and the refusal that quotes it can never name different paths.
/// </summary>
public sealed record BootstrapPaths(string ContextYaml, string CodingPrinciples)
{
    public static BootstrapPaths For(string contextName)
    {
        var metaDir = ProjectMetaPaths.MetaDirFor(contextName);
        return new BootstrapPaths(
            $"{metaDir}/{ProjectMetaPaths.ContextYamlFile}",
            $"{metaDir}/{ProjectMetaPaths.CodingPrinciplesFile}");
    }

    public IReadOnlyList<string> All => [ContextYaml, CodingPrinciples];
}
