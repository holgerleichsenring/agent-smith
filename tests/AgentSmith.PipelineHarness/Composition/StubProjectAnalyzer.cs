using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199b: returns a canned csharp ProjectMap so TestHandler picks up the
/// real `dotnet test` command. Bypasses the LLM-driven analyzer so the
/// ScriptedChatClient's FIFO isn't polluted by the analyzer's scout-tool
/// calls (which would steal queued tool-calls intended for AgenticMaster).
/// </summary>
internal sealed class StubProjectAnalyzer : IProjectAnalyzer
{
    public Task<ProjectMap> AnalyzeAsync(
        string repositoryPath, AgentConfig agent, ISandbox sandbox,
        CancellationToken cancellationToken, string? repoName = null) =>
        Task.FromResult(new ProjectMap(
            PrimaryLanguage: "csharp",
            Frameworks: new[] { "dotnet8" },
            Modules: new[] { new Module(".", ModuleRole.Production, Array.Empty<string>()) },
            TestProjects: new[] { new TestProject("Fixture.Tests", "xunit", 1, null) },
            EntryPoints: Array.Empty<string>(),
            Conventions: new Conventions(null, null, null),
            Ci: new CiConfig(
                HasCi: false,
                BuildCommand: "dotnet build",
                TestCommand: "dotnet test Fixture.Tests/Fixture.Tests.csproj",
                CiSystem: null)));
}
