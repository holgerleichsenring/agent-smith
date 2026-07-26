using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Memory;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AgentSmith.Tests.Memory;

/// <summary>
/// p0380: recall joins EVERY master surface including the read-only Review
/// surface; remember joins as a proposal tool that writes ONLY memory paths —
/// which are run-record-class, so they never count as code changes for the
/// keystone.
/// </summary>
public sealed class MemorySurfaceTests
{
    private readonly InMemorySandboxFileReader _reader = new();
    private readonly MemoryRecallToolHost _recall;
    private readonly MemoryWriteToolHost _remember;

    public MemorySurfaceTests()
    {
        var store = new MemoryStore(_reader, "/work");
        _recall = new MemoryRecallToolHost(store);
        _remember = new MemoryWriteToolHost(store);
    }

    [Fact]
    public void ReviewSurface_IncludesRecall_RememberIsProposalOnly_NoCodeWrite()
    {
        var fs = new FilesystemToolHost(new Mock<ISandbox>().Object);
        var log = new LogDecisionToolHost(new StubDecisionLogger());

        var tools = AgenticToolSurface.Review(fs, log, web: null, _recall, _remember)
            .OfType<AIFunction>().Select(t => t.Name).ToHashSet();

        tools.Should().Contain("recall", "a memory read joins the read-only scan surface");
        tools.Should().Contain("remember", "a scan master may PROPOSE dismissal memories");
        tools.Should().NotContain("write_file");
        tools.Should().NotContain("edit");
        tools.Should().NotContain("run_command");
    }

    [Fact]
    public void ReadWriteSurface_IncludesRecallAndRemember()
    {
        var fs = new FilesystemToolHost(new Mock<ISandbox>().Object);
        var log = new LogDecisionToolHost(new StubDecisionLogger());
        var human = new HumanToolHost(null, null);

        var tools = AgenticToolSurface.ReadWriteWithHuman(
                fs, log, human, recall: _recall, remember: _remember)
            .OfType<AIFunction>().Select(t => t.Name).ToHashSet();

        tools.Should().Contain("recall").And.Contain("remember");
    }

    [Fact]
    public async Task Remember_WritesRunRecordClassPathsOnly_NeverCodeChangesForKeystone()
    {
        await _remember.Remember("project", "some-fact", "a fact", "body");

        _reader.Files.Keys.Should().NotBeEmpty();
        foreach (var absolute in _reader.Files.Keys)
        {
            var repoRelative = absolute["/work/".Length..];
            RunRecordPaths.IsRunRecordPath(repoRelative).Should().BeTrue(
                $"'{repoRelative}' must be run-record-class so memory writes never trip the keystone");
        }
    }

    [Fact]
    public void MemoryPaths_AreRunRecordClass_PinnedAgainstKeystone()
    {
        // p0322c made IsRunRecordPath match ALL of .agentsmith/ — pin that the
        // memory store is inside it, and that a memory-only diff still counts
        // as "no real source change" for the apply-drive/keystone logic.
        RunRecordPaths.IsRunRecordPath(".agentsmith/memory/ticket-42.md").Should().BeTrue();
        RunRecordPaths.IsRunRecordPath(".agentsmith/memory/MEMORY.md").Should().BeTrue();
        RunRecordPaths.IsRunRecordPath("repo/.agentsmith/memory/x.md").Should().BeTrue();

        var memoryOnlyChanges = new List<CodeChange>
        {
            new(new FilePath(".agentsmith/memory/ticket-42.md"), "body", "Create"),
            new(new FilePath(".agentsmith/memory/MEMORY.md"), "index", "Update")
        };
        Application.Services.Handlers.AgenticMasterHandler
            .ShouldDriveApply("fix-bug", memoryOnlyChanges)
            .Should().BeTrue("memory-only writes are not code changes");
    }

    private sealed class StubDecisionLogger : IDecisionLogger
    {
        public Task LogAsync(string? repoPath, DecisionCategory category, string decision,
                             CancellationToken cancellationToken = default, string? sourceLabel = null)
            => Task.CompletedTask;
    }
}
