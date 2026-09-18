using AgentSmith.Application.Services.Metrics;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-09-17-042eg: the start-and-confirm collaborator over the REAL project resolver, because
/// what it answers is exactly what the poller would answer — a double that said "routed" would
/// prove nothing about the ticket a run has to pick up.
/// <para>
/// The default config has no projects at all, so nothing routes and nothing is ever moved: what
/// every pre-existing filing test wants, since a filing that started nothing is the shape they
/// were written against. A test about starting passes a routing config of its own.
/// </para>
/// </summary>
internal static class FiledWorkDoubles
{
    internal static FiledWorkStarter Starter(
        AgentSmithConfig? routing = null, IStartupFindings? findings = null) =>
        new(routing ?? new AgentSmithConfig(),
            new ProjectResolver(
                new AgentSmithMetrics(), new PipelineResolver(),
                NullLogger<ProjectResolver>.Instance, findings),
            NullLogger<FiledWorkStarter>.Instance, findings);
}
