using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>2026-10-13-3890b: result.md's change reader, wired the way the container wires it.</summary>
public static class TestRunResultChanges
{
    public static RunResultChanges Create() => new(
        new BranchChangedFiles(
            new SandboxBaseLadder(
                new SandboxBaseBranch(NullLogger<SandboxBaseBranch>.Instance),
                NullLogger<SandboxBaseLadder>.Instance),
            new SandboxRunStartCommit(NullLogger<SandboxRunStartCommit>.Instance),
            NullLogger<BranchChangedFiles>.Instance),
        new SandboxTargets(), NullLogger<RunResultChanges>.Instance);
}
