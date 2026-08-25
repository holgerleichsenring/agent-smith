using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Reads one sandbox's contexts for the two bootstrap files and says which paths it read.
/// Extracted from <see cref="BootstrapCheckHandler"/> by p0496: the handler decides what
/// the run does about the answer, this decides what the answer is.
/// </summary>
public sealed class BootstrapContextProbe(
    ISandboxFileReaderFactory readerFactory, ILogger<BootstrapContextProbe> logger)
{
    public async Task<(bool Context, bool Principles)> ProbeAsync(
        ISandbox sandbox, string key,
        IReadOnlyList<RemoteContextDiscovery> contextsInSandbox, CancellationToken ct)
    {
        var allContext = true;
        var allPrinciples = true;
        foreach (var discovery in contextsInSandbox)
        {
            var (context, principles) = await ProbeOneAsync(sandbox, key, discovery, ct);
            allContext &= context;
            allPrinciples &= principles;
        }
        return (allContext, allPrinciples);
    }

    /// <summary>The paths this probe reads for the given contexts, in the order it reads them.</summary>
    public static IReadOnlyList<string> PathsFor(IEnumerable<RemoteContextDiscovery> contexts) =>
        [.. contexts.SelectMany(d => BootstrapPaths.For(d.ContextName).All)];

    private async Task<(bool Context, bool Principles)> ProbeOneAsync(
        ISandbox sandbox, string key, RemoteContextDiscovery discovery, CancellationToken ct)
    {
        var paths = BootstrapPaths.For(discovery.ContextName);
        var reader = readerFactory.Create(sandbox);
        var context = await reader.ExistsAsync(paths.ContextYaml, ct);
        var principles = await reader.ExistsAsync(paths.CodingPrinciples, ct);
        logger.LogInformation(
            "Probe {Key}/{Context}: context.yaml={CtxOk} principles={PrincOk} (path={MetaDir}/)",
            key, discovery.ContextName, context, principles,
            ProjectMetaPaths.MetaDirFor(discovery.ContextName));
        return (context, principles);
    }
}
