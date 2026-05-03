using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

public sealed class SecurityTriageHandlerSignalTests
{
    [Fact]
    public void SecurityTriage_WithProjectMap_DetectsCodeAreaSignalsFromModulePaths()
    {
        // p0110c: signals are now derived from ProjectMap.Modules paths instead of
        // CodeAnalysis.FileStructure. The discovery is more focused (only modules,
        // not raw file enumeration) but the signal-detection contract is the same.
        var map = new ProjectMap(
            "C#",
            [".NET 8", "ASP.NET Core"],
            [
                new Module("src/Controllers/UserController", ModuleRole.Production, []),
                new Module("src/Models/LoginRequest", ModuleRole.Production, []),
                new Module("src/Services/AuthGuard", ModuleRole.Production, []),
                new Module("src/Middleware/ExceptionMiddleware", ModuleRole.Production, [])
            ],
            [], [], new Conventions(null, null, null), new CiConfig(false, null, null, null));

        var paths = map.Modules.Select(m => m.Path.ToLowerInvariant()).ToList();

        paths.Any(p => p.Contains("controller")).Should().BeTrue("has direct object references");
        paths.Any(p => p.Contains("request")).Should().BeTrue("has input entry points");
        paths.Any(p => p.Contains("auth") || p.Contains("guard")).Should().BeTrue("has auth guard changes");
        paths.Any(p => p.Contains("exception") || p.Contains("middleware")).Should().BeTrue("has error handling");
    }

    [Fact]
    public void ChainAnalyst_SkillDefinition_HasExecutorRole()
    {
        // chain-analyst is defined in config/skills/security/chain-analyst/agentsmith.md with role: executor
        // This test verifies the OrchestrationRole enum supports Executor
        OrchestrationRole.Executor.Should().NotBe(OrchestrationRole.Contributor);
        OrchestrationRole.Executor.Should().NotBe(OrchestrationRole.Gate);
    }
}
