using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Tools;

/// <summary>2026-11-25-9992c: the prompt names each repository at /work/&lt;repo&gt;, so the file tools route that
/// form like the repo-prefixed one.</summary>
public sealed class WorkspacePathRoutingTests
{
    private readonly StepRecordingSandbox _api = new();
    private readonly StepRecordingSandbox _web = new();

    private FilesystemToolHost Host() => new(
        new Dictionary<string, ISandbox>(StringComparer.Ordinal)
        {
            ["api"] = new RepoSandbox(_api, "api"),
            ["web"] = new RepoSandbox(_web, "web"),
        },
        defaultRepo: "api");

    [Fact]
    public async Task ReadFile_WorkspaceRootedPath_RoutesToThatRepository()
    {
        await Host().ReadFile("/work/web/src/app.ts");

        _web.Steps.Should().ContainSingle().Which.Path.Should().Be("/work/web/src/app.ts");
        _api.Steps.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadFile_WorkspaceRootedPathOfNoRepository_IsRefusedOnAMultiRepoRun()
    {
        var read = await Host().ReadFile("/work/other/x.cs");

        read.Should().StartWith("Error: path '/work/other/x.cs' must start with a repo name");
    }
}
