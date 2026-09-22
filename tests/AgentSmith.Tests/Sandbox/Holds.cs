using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-2d11b: hold registers for tests that are not about holding. The empty one is
/// what every caller that names no conversation sees: nothing is ever taken and nothing is
/// ever kept, so the code under test behaves as it did before a hold existed.
/// </summary>
internal static class Holds
{
    /// <summary>A register with a heartbeat that answers NO — so nothing is ever handed out.</summary>
    public static IHeldSandboxRegister None() =>
        new HeldSandboxRegister(new StubHeartbeat(alive: false), NullLogger<HeldSandboxRegister>.Instance);

    /// <summary>A register whose held sandboxes are all alive — what a live conversation sees.</summary>
    public static IHeldSandboxRegister Live() =>
        new HeldSandboxRegister(new StubHeartbeat(alive: true), NullLogger<HeldSandboxRegister>.Instance);
}
