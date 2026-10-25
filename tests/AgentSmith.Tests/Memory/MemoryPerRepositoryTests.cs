using AgentSmith.Application.Services.Tools;
using AgentSmith.Tests.Repositories;
using FluentAssertions;

namespace AgentSmith.Tests.Memory;

/// <summary>
/// 2026-10-12-f5f7c: a memory lives in the repository it is about — remember names it when the
/// run has more than one, recall reads them all, and a repository whose commit will not land
/// this run is answered instead of written.
/// </summary>
public sealed class MemoryPerRepositoryTests
{
    private const string Root = "/work";
    private const string EntryPath = "/work/.agentsmith/memory/build-order.md";
    private const string Changed = " M src/Program.cs\n";

    private readonly RunRepositoriesTestRun _run = new();

    private MemoryWriteToolHost Remember() => new(_run.Repositories(), Root);

    [Fact]
    public async Task MemoryWriteToolHost_TwoRepos_WritesIntoNamedRepo()
    {
        var server = _run.Add("server", "server", Changed);
        var client = _run.Add("client", "client", Changed);

        var reply = await Remember().Remember("project", "build-order", "Contracts first", "Body.", "server");

        reply.Should().Contain("recorded in server");
        server.Files.Should().ContainKey(EntryPath);
        client.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task MemoryWriteToolHost_TwoReposNoneNamed_IsToolError()
    {
        var server = _run.Add("server", "server", Changed);
        var client = _run.Add("client", "client", Changed);

        var reply = await Remember().Remember("project", "build-order", "Contracts first", "Body.");

        reply.Should().StartWith("Error:").And.Contain("server, client");
        server.Files.Should().BeEmpty();
        client.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task MemoryWriteToolHost_OneRepoTwoToolchainSandboxes_NeedsNoName()
    {
        var primary = _run.Add("server-csharp", "server", Changed);
        var secondary = _run.Add("server-typescript", "server", Changed);

        var reply = await Remember().Remember("project", "build-order", "Contracts first", "Body.");

        reply.Should().Contain("recorded in server");
        primary.Files.Should().ContainKey(EntryPath, "the sandbox the repository commits from");
        secondary.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task MemoryWriteToolHost_RepoAddedMidRun_WritesThere()
    {
        _run.Add("server", "server", Changed);
        var host = Remember();

        var added = _run.Add("client", "client", Changed);
        var reply = await host.Remember("project", "build-order", "Contracts first", "Body.", "client");

        reply.Should().Contain("recorded in client");
        added.Files.Should().ContainKey(EntryPath);
    }

    [Fact]
    public async Task MemoryWriteToolHost_RepoWithoutPrThisRun_AnswersInstead()
    {
        var server = _run.Add("server", "server", Changed);
        var client = _run.Add("client", "client", porcelain: "?? .agentsmith/memory/old.md\n");

        var reply = await Remember().Remember("project", "build-order", "Contracts first", "Body.", "client");

        reply.Should().StartWith("Not recorded").And.Contain("'client'").And.Contain("[server]");
        client.Files.Should().BeEmpty();
        server.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task MemoryRecallToolHost_TwoRepos_ListsBothLabelled()
    {
        _run.Add("server", "server", Changed);
        _run.Add("client", "client", Changed);
        var remember = Remember();
        await remember.Remember("project", "server-build", "How the server builds", "dotnet build.", "server");
        await remember.Remember("project", "client-build", "How the client builds", "pnpm build.", "client");

        var result = await new MemoryRecallToolHost(_run.Repositories(), Root, AgentSmith.Tests.TestHelpers.MemoryStandings.Reader(), new AgentSmith.Contracts.Commands.PipelineContext()).Recall("builds");

        result.Should().Contain("## server-build (project, unconfirmed · no record yet) — repository server")
            .And.Contain("## client-build (project, unconfirmed · no record yet) — repository client")
            .And.Contain("dotnet build.").And.Contain("pnpm build.");
    }
}
