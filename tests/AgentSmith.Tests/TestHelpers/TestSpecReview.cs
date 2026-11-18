using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-09-17-042eh: a spec's start heads, wired the way the container wires them,
/// so a test asserting behaviour does not restate six constructors to get at one.
/// </summary>
internal static class TestSpecReview
{
    public static SpecStartHeads StartHeads(SandboxTargets? targets = null) =>
        new(targets ?? new SandboxTargets(), NullLogger<SpecStartHeads>.Instance);
}
