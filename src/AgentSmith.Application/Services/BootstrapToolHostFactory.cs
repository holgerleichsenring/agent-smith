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
/// Builds the bootstrap-round tool surface (writes limited to .agentsmith/*
/// via the Bootstrap-phase PathWriteGuard) and produces the AITool list the
/// chat client sees. Returns a <see cref="BootstrapToolBundle"/> exposing the
/// tools plus accessors for the writes / decisions the hosts accumulated.
/// </summary>
public sealed class BootstrapToolHostFactory(IDecisionLogger decisionLogger)
{
    public BootstrapToolBundle Create(ISandbox sandbox, string repoLocalPath)
    {
        var readGuard = new PathReadGuard(NullGitIgnoreResolver.Instance, () => repoLocalPath);
        var writeGuard = new PathWriteGuard(readGuard, SkillExecutionPhase.Bootstrap);
        var fs = new FilesystemToolHost(sandbox, repoLocalPath, readGuard, writeGuard);
        var log = new LogDecisionToolHost(decisionLogger, repoLocalPath);
        var tools = AgenticToolSurface.Bootstrap(fs, log);
        return new BootstrapToolBundle(tools, fs.GetChanges, log.GetDecisions);
    }

    private sealed class NullGitIgnoreResolver : IGitIgnoreResolver
    {
        public static readonly NullGitIgnoreResolver Instance = new();
        public bool IsIgnored(string fullPath, string repoPath) => false;
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
