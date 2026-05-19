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
/// Builds the bootstrap-round SandboxToolHost (writes limited to .agentsmith/*
/// via the Bootstrap-phase PathWriteGuard) and produces the AITool list the
/// chat client sees. Returns a <see cref="BootstrapToolBundle"/> exposing the
/// tools plus accessors for the writes / decisions the host accumulated, so
/// callers never need to name the obsolete SandboxToolHost type directly.
/// </summary>
public sealed class BootstrapToolHostFactory(IDecisionLogger decisionLogger)
{
    public BootstrapToolBundle Create(ISandbox sandbox, string repoLocalPath)
    {
        var readGuard = new PathReadGuard(NullGitIgnoreResolver.Instance, () => repoLocalPath);
        var writeGuard = new PathWriteGuard(readGuard, SkillExecutionPhase.Bootstrap);
#pragma warning disable CS0618 // SandboxToolHost is obsolete; bootstrap still needs the explicit guards
        var host = new SandboxToolHost(
            sandbox, decisionLogger, dialogueTransport: null, jobId: null,
            repoPath: repoLocalPath, readGuard: readGuard, writeGuard: writeGuard);
#pragma warning restore CS0618
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(host.ReadFile),
            AIFunctionFactory.Create(host.WriteFile),
            AIFunctionFactory.Create(host.ListFiles),
            AIFunctionFactory.Create(host.Grep),
            AIFunctionFactory.Create(host.LogDecision),
        };
        return new BootstrapToolBundle(tools, host.GetChanges, host.GetDecisions);
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
