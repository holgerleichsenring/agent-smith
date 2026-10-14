using AgentSmith.Application;
using AgentSmith.Application.Services.Resume;
using AgentSmith.Application.Services.SpecExecution;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Extensions;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-13-8762p: the record step's copy of a ticket's decided defaults — once per default, into the recorded
/// spec's decision file or its run's in the repository of the set's first spec, and nothing without a database.
/// </summary>
public sealed class DefaultVerdictRecordTests
{
    private const string A = "2026-10-14-5678a";
    private const string B = "2026-10-14-5678b";
    private const string RunId = "2026-10-13T09-00-00-0001";
    private static readonly RepoConnection Shop = new() { Name = "shop", Type = RepoType.Local };
    private static readonly RepoConnection Web = new() { Name = "web", Type = RepoType.Local };
    private static readonly DateTimeOffset At = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    private static DecidedDefault Row(long id, string? specId) =>
        new(id, RunId, specId, "Which queue?", "polling", DefaultDecisionVerdicts.Ratified, null, "ana", At);

    private static DerivedSpec Spec(string id, string repo) =>
        new(new SpecDraft(id, $"Goal {id}", $"spec: {id}\n", []) { Repos = [repo] }, id, string.Empty, []);

    private static SpecDoneRecord Record(RepoConnection carrier) => new(carrier,
        new SpecSet("github-7", [Spec(A, "shop"), Spec(B, "web")], SpecAccounting.Empty,
            [new SpecRevision(1, "initial derivation", At)], SpecSource.Derived),
        Spec(carrier == Shop ? A : B, carrier.Name!), "body", "line", MultiRepo: true);

    private static PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectName, "alpha");
        pipeline.Set(ContextKeys.TicketId, new TicketId("7"));
        return pipeline;
    }

    private static DefaultVerdictRecord Recorder(params DecidedDefault[] rows)
    {
        var decided = new Mock<IDecidedDefaults>();
        decided.Setup(d => d.ReadAsync("alpha", "7", null, It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        return new DefaultVerdictRecord(decided.Object, new RepositoryVerdictFile(NullLogger<RepositoryVerdictFile>.Instance),
            NullLogger<DefaultVerdictRecord>.Instance);
    }

    [Fact]
    public async Task DefaultVerdictRecord_RowAlreadyInTheFile_IsNotWrittenTwice()
    {
        var files = new SpecBranchFiles();
        var recorder = Recorder(Row(3, A));

        var first = await recorder.WriteAsync(Pipeline(), files, Record(Shop), default);
        var second = await recorder.WriteAsync(Pipeline(), files, Record(Shop), default);

        first.Should().Equal($".agentsmith/decisions/{A}.yaml");
        second.Should().BeEmpty("the file already holds [default #3]");
        var content = await files.ReadRequiredAsync($".agentsmith/decisions/{A}.yaml", default);
        content.Should().StartWith($"spec: {A}\ndecisions:\n");
        content.Split("[default #3]").Should().HaveCount(2, "the entry is in the file exactly once");
    }

    [Fact]
    public async Task DefaultVerdictRecord_RowNamingNoSpecOfTheSet_GoesToTheRunsDecisionFile_InTheFirstSpecsRepository()
    {
        var shop = new SpecBranchFiles();
        var web = new SpecBranchFiles();
        var recorder = Recorder(Row(3, null), Row(4, "2026-01-01-0000a"), Row(5, B));

        (await recorder.WriteAsync(Pipeline(), shop, Record(Shop), default)).Should().Equal($".agentsmith/decisions/{RunId}.yaml");
        (await recorder.WriteAsync(Pipeline(), web, Record(Web), default)).Should().Equal($".agentsmith/decisions/{B}.yaml");

        var run = await shop.ReadRequiredAsync($".agentsmith/decisions/{RunId}.yaml", default);
        run.Should().StartWith($"run: {RunId}").And.Contain("[default #3]").And.Contain("[default #4]").And.NotContain("[default #5]");
        web.Paths.Should().ContainSingle("the second repository holds its own spec's row only");
    }

    [Fact]
    public async Task SpecDoneRecorder_WithoutADatabase_ResolvesAndRecordsNoVerdict()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddAgentSmithInfrastructure();
        services.AddAgentSmithCommands();
        services.AddInProcessSandbox();
        services.AddSingleton(Mock.Of<IDialogueTransport>());
        services.AddSingleton(Mock.Of<IProgressReporter>());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<SpecDoneRecorder>().Should().NotBeNull();
        provider.GetRequiredService<IDecidedDefaults>().Should().BeOfType<NullDecidedDefaults>();
        provider.GetRequiredService<IDefaultVerdictFile>().Should().BeOfType<RepositoryVerdictFile>();
        var files = new SpecBranchFiles();
        (await provider.GetRequiredService<DefaultVerdictRecord>().WriteAsync(Pipeline(), files, Record(Shop), default))
            .Should().BeEmpty();
        files.Paths.Should().BeEmpty();
    }
}
