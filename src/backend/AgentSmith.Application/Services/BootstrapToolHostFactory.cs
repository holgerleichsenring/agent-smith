using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services;

/// <summary>
/// Builds the bootstrap-round tool surface (writes limited to the round's
/// per-context MetaDir via the Bootstrap-phase PathWriteGuard) and produces the
/// AITool list the chat client sees. Returns a <see cref="BootstrapToolBundle"/>
/// exposing the tools plus accessors for the writes / decisions the hosts
/// accumulated.
///
/// p0161d: <c>contextName</c> scopes the write guard to
/// <c>.agentsmith/contexts/&lt;contextName&gt;/{context.yaml,coding-principles.md}</c>
/// — never the flat root path, never a foreign context's path. Empty string
/// falls back to the legacy flat layout for pre-p0161d test fixtures.
/// </summary>
public sealed class BootstrapToolHostFactory(
    IDecisionLogger decisionLogger,
    IPathReadGuard readGuard,
    IPathWriteGuard writeGuard)
{
    public BootstrapToolBundle Create(ISandbox sandbox, string repoLocalPath, string contextName = "")
    {
        var fs = new FilesystemToolHost(
            sandbox, repoLocalPath, readGuard, writeGuard,
            writePhase: SkillExecutionPhase.Bootstrap,
            contextName: contextName);
        var log = new LogDecisionToolHost(decisionLogger, repoLocalPath);
        var tools = AgenticToolSurface.Bootstrap(fs, log);
        return new BootstrapToolBundle(tools, fs.GetChanges, log.GetDecisions);
    }
}

/// <summary>
/// Outcome of a bootstrap tool-host build: the tool list passed to the chat
/// client plus accessors for the file-changes and decision-log entries
/// produced during the call. Capturing accessors (not snapshots) lets the
/// caller invoke them after the chat completes.
/// </summary>
public sealed record BootstrapToolBundle(
    IList<AITool> Tools,
    Func<IReadOnlyList<CodeChange>> GetChanges,
    Func<IReadOnlyList<PlanDecision>> GetDecisions);
