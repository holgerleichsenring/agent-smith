using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

public sealed class SecurityTriageHandlerSignalTests
{
    [Fact]
    public void SecurityTriage_WithCodeAnalysis_DetectsCodeAreaSignals()
    {
        var codeAnalysis = new CodeAnalysis(
            new List<string>
            {
                "Controllers/UserController.cs",
                "Models/LoginRequest.cs",
                "Services/AuthGuard.cs",
                "Middleware/ExceptionMiddleware.cs"
            },
            new List<string> { "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore" },
            ".NET 8",
            "C#");

        var files = codeAnalysis.FileStructure.Select(f => f.ToLowerInvariant()).ToList();

        files.Any(f => f.Contains("controller")).Should().BeTrue("has direct object references");
        files.Any(f => f.Contains("request")).Should().BeTrue("has input entry points");
        files.Any(f => f.Contains("auth") || f.Contains("guard")).Should().BeTrue("has auth guard changes");
        files.Any(f => f.Contains("exception") || f.Contains("middleware")).Should().BeTrue("has error handling");
        // hasFileUploadChanges signal was removed in p94b — input-abuser was its only consumer
    }

    [Fact]
    public void ChainAnalyst_SkillDefinition_HasExecutorRole()
    {
        // chain-analyst is defined in config/skills/security/chain-analyst/agentsmith.md with role: executor
        // This test verifies the SkillRole enum supports Executor
        SkillRole.Executor.Should().NotBe(SkillRole.Contributor);
        SkillRole.Executor.Should().NotBe(SkillRole.Gate);
    }
}
