using AgentSmith.Application.Services.Repositories;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Repositories;

public sealed class RunRepositoriesTests
{
    private readonly RunRepositoriesTestRun _run = new();

    [Fact]
    public void Names_TwoToolchainSandboxesOfOneRepository_OneName()
    {
        _run.Add("server-csharp", "server");
        _run.Add("server-typescript", "server");

        _run.Repositories().Names().Should().Equal("server");
    }

    [Fact]
    public void Names_TemplateAndReferenceScopes_AreNoRepositories()
    {
        _run.Add("server", "server");
        _run.Add("template:api", "template:api");
        _run.Add("reference:site", "reference:site");

        _run.Repositories().Names().Should().Equal("server");
        _run.Repositories().FilesOf("template:api").Should().BeNull();
    }

    [Fact]
    public void FilesOf_IsTheFirstSandboxTheRepositoryOwns_TheOneItCommitsFrom()
    {
        var primary = _run.Add("server-csharp", "server");
        _run.Add("server-typescript", "server");

        _run.Repositories().FilesOf("server").Should().BeSameAs(primary);
        _run.Repositories().FilesOf("client").Should().BeNull();
    }

    [Fact]
    public async Task HasCodeChange_OnlyRunRecordPaths_IsFalse_CodePath_IsTrue()
    {
        _run.Add("server", "server", porcelain: "?? .agentsmith/memory/x.md\n");
        _run.Add("client", "client", porcelain: " M src/App.ts\n");
        var repositories = _run.Repositories();

        (await repositories.HasCodeChangeAsync("server", CancellationToken.None)).Should().BeFalse();
        (await repositories.HasCodeChangeAsync("client", CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task HasCodeChange_CodeAlreadyCheckpointed_IsTrue()
    {
        _run.Add("server", "server");
        _run.Pipeline.Set(ContextKeys.CheckpointedRepos, new Dictionary<string, bool> { ["server"] = true });

        (await _run.Repositories().HasCodeChangeAsync("server", CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public void Pick_NamedOneUnnamedWithOneOrNone_AndTheTwoErrors()
    {
        _run.Add("server", "server");
        RunRepositoryPick.From(_run.Repositories().Names(), null).Name.Should().Be("server");
        RunRepositoryPick.From(_run.Repositories().Names(), "nope").Error.Should().Contain("unknown repository 'nope'");

        _run.Add("client", "client");
        RunRepositoryPick.From(_run.Repositories().Names(), "CLIENT").Name.Should().Be("client");
        RunRepositoryPick.From(_run.Repositories().Names(), null).Error.Should().Contain("2 repositories").And.Contain("server, client");

        RunRepositoryPick.From([], null)
            .Should().Be(new RunRepositoryPick(null, null));
    }

    [Fact]
    public async Task FixedRunRepositories_EveryListedRepositoryLands()
    {
        var reader = new InMemorySandboxFileReader();
        var fixedSet = TestRepositories.One(reader, "server");

        fixedSet.Names().Should().Equal("server");
        fixedSet.FilesOf("server").Should().BeSameAs(reader);
        (await fixedSet.HasCodeChangeAsync("server", CancellationToken.None)).Should().BeTrue();
        (await fixedSet.HasCodeChangeAsync("client", CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public void Factory_For_ReadsThePipelineAtEachCall()
    {
        var repositories = _run.Factory().For(_run.Pipeline);
        repositories.Names().Should().BeEmpty();

        _run.Add("server", "server");

        repositories.Names().Should().Equal("server");
    }
}
