using AgentSmith.Contracts.Pocs;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>
/// 2026-11-23-933db: a server-provisioned PoC project runs only the PoC presets (operator ruling C). Called in the run
/// prologue right after the preset resolves, so a refused run never announces itself. A project counts only when its key
/// starts with 'poc-' AND a Poc row names it — an operator's own poc-* project stays ordinary. It holds whether PoCs are
/// switched on or off (open question guard-when-off, kept on).
/// </summary>
public sealed class PocProjectPresetGuard(IPocProjects projects)
{
    public async Task CheckAsync(string project, string pipeline, CancellationToken ct)
    {
        if (PocPresets.Contains(pipeline)) return;
        if (!project.StartsWith(IPocProjects.Prefix, StringComparison.OrdinalIgnoreCase)) return;
        if (await projects.IsAsync(project, ct))
            throw new ConfigurationException($"The '{pipeline}' run is refused in project '{project}': poc- projects run only "
                + $"the PoC presets ({string.Join(", ", PocPresets.Names)}).");
    }
}
