using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-09-0620c: how many source files a review master must read before its review counts as covering the
/// source. A pull-request review judges a diff and reads around it only where a judgement needs it, so it has no floor:
/// re-driving it to inventory the whole surface turned every small PR into a repository scan.
/// </summary>
internal static class ScanReadFloor
{
    internal static int Of(string master, AgentConfig agent) =>
        master == PipelinePresets.MasterFor("pr-review") ? 0 : agent.ScanMinSourceReads;
}
