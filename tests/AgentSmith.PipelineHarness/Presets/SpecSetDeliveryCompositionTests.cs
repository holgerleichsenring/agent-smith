using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-28-db88a end to end through the REAL server composition: a series of two tickets over two repositories is
/// FILED (filing's branch write, the stored approval), each ticket is CLAIMED with the approval its claim carries and
/// BUILT spec by spec, its work is merged into the series branch and its ticket branch deleted, and the series check reads
/// every ticket's criteria for the final report. What the final pull request delivers is what reached the branches —
/// every file filing wrote and every file a run wrote into a repository's tree — and none of it is a series manifest or a
/// run record: code, specs, decisions, memory and the done index only.
/// <para>
/// The stub harness has no git: the merge into the series branch is the ticket branches' files put on it, and opening the
/// final pull request adds no file of its own (it is a pull request from that branch), so the branch contents are asserted
/// where they are written. The beat that opens it needs tracker facts this test does not run; the series check is driven
/// directly.
/// </para>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecSetDeliveryCompositionTests
{
    private const string Project = "fixture";
    private const string Tracker = "github-fixture";
    private const string SeriesBranch = "agent-smith/series/sr-1";

    private static readonly IReadOnlyList<RepoConnection> Repos =
    [
        new() { Name = "sample-client", Type = RepoType.Local, Path = "/tmp", Url = "https://stub.test/sample-client" },
        new() { Name = "sample-server", Type = RepoType.Local, Path = "/tmp", Url = "https://stub.test/sample-server" },
    ];

    private static readonly ResolvedProject Host = new()
    {
        Name = Project, Repos = Repos, Tracker = new TrackerConnection { Name = Tracker, Type = TrackerType.GitHub },
    };

    [Fact]
    public async Task Delivered_NoBranchCarriesASeriesManifest()
    {
        var branches = new RecordingBranchSources();
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ISourceProviderFactory>().AddSingleton<ISourceProviderFactory>(new BranchRecordingSourceFactory(branches));
        });
        var first = await FileAsync(harness, "1", "2026-11-28-01a0",
            new HarnessSeries.Member("Introduce the payload guard", "An empty body is answered with 400.", "sample-server"),
            new HarnessSeries.Member("Show the guard's error", "The client shows the 400 message.", "sample-client", "show"));
        var second = await FileAsync(harness, "2", "2026-11-28-02a0",
            new HarnessSeries.Member("Log rejected payloads", "A rejected payload is logged once.", "sample-server", "log"));

        await BuildAsync(harness, "1", first, "sample-server/src/Guard.cs", "sample-client/src/GuardError.ts");
        await BuildAsync(harness, "2", second, "sample-server/src/RejectLog.cs");
        foreach (var write in branches.Writes.ToList()) branches.Seed(write.Repo, SeriesBranch, write.Files); // the merges
        foreach (var id in new[] { "1", "2" }) await LandAsync(harness, branches, id);
        var (criteria, unread) = await CheckAsync(harness);

        unread.Should().BeEmpty();
        criteria.Select(c => c.Text).Should().Equal(
            "An empty body is answered with 400.", "The client shows the 400 message.", "A rejected payload is logged once.");
        var delivered = Delivered(harness, branches);
        delivered.Should().Contain(["sample-server/src/Guard.cs", "sample-client/src/GuardError.ts", "sample-server/src/RejectLog.cs"]);
        delivered.Should().Contain(p => p.Contains("/.agentsmith/specs/done/2026-11-28-01a0a-", StringComparison.Ordinal));
        delivered.Should().Contain(p => p.Contains("/.agentsmith/specs/done/2026-11-28-02a0a-", StringComparison.Ordinal));
        delivered.Should().NotContain(p => p.Contains(".agentsmith/series/", StringComparison.Ordinal), "no manifest is written");
        delivered.Should().NotContain(p => p.Contains(".agentsmith/runs/", StringComparison.Ordinal), "no run record is written");
        delivered.Where(p => !IsDeliverable(p)).Should().BeEmpty("the delivery is code, specs, decisions, memory and done.md");
    }

    // The two repositories' trees and filing's writes — what reaches a branch — repository-relative after the repo name.
    private static List<string> Delivered(RealCompositionHarness harness, RecordingBranchSources branches)
    {
        var steps = harness.StubSandboxFactory!.Spawned.SelectMany(s => s.Sandbox.RanSteps)
            .Where(s => s.Kind == StepKind.WriteFile && s.Path is { } p && Repos.Any(r => p.StartsWith($"/work/{r.Name}/", StringComparison.Ordinal)))
            .Select(s => s.Path!["/work/".Length..]);
        return [.. steps.Concat(branches.Writes.SelectMany(w => w.Paths.Select(p => $"{w.Repo}/{p}"))).Distinct()];
    }

    private static bool IsDeliverable(string path)
    {
        var inRepo = path[(path.IndexOf('/') + 1)..];
        return inRepo.StartsWith("src/", StringComparison.Ordinal)
            || inRepo.StartsWith(".agentsmith/specs/", StringComparison.Ordinal)
            || inRepo.StartsWith(".agentsmith/decisions/", StringComparison.Ordinal)
            || inRepo.StartsWith(".agentsmith/memory/", StringComparison.Ordinal)
            || (inRepo.StartsWith(".agentsmith/contexts/", StringComparison.Ordinal) && inRepo.EndsWith("/done.md", StringComparison.Ordinal))
            || inRepo == ".gitattributes"; // 2026-11-26-462aa: done.md merges with merge=union
    }

    // Filing's branch write and the stored approval, as the 'file' run leaves a ticket.
    private static async Task<SpecApprovalRecord> FileAsync(
        RealCompositionHarness harness, string id, string seriesBase, params HarnessSeries.Member[] members)
    {
        var key = TicketKey.For("github", id).Value;
        // The run's tracker connection carries no catalog name in the harness, so the record names none: the claim's carried
        // approval is the run's own ticket's and is not ignored.
        var record = new SpecApprovalRecord(key, HarnessSeries.SetOf(key, members, seriesBase), [.. Repos.Select(r => r.Name)],
            string.Empty, members[0].Repo, id) { Project = Project };
        await harness.Services.GetRequiredService<ISpecApprovalStore>().SaveAsync(record, default);
        var filed = await harness.Services.GetRequiredService<FiledSpecBranch>().WriteAsync(Host, record, new TicketId(id),
            new Ticket(new TicketId(id), $"Ticket {id}", "body", null, "open", "github", []), default);
        filed.Written.Should().BeTrue(filed.Error);
        return record;
    }

    // The ticket's claim — its approval on the initial context — and its build, one master pass per spec.
    private static async Task BuildAsync(RealCompositionHarness harness, string id, SpecApprovalRecord record, params string[] files)
    {
        harness.ChatClient.EnqueueScopeReply("no verdict"); // the run keeps both repositories
        foreach (var file in files)
            harness.ChatClient
                .EnqueueText($"Planning: {file}.")
                .EnqueueToolCall("write_file", $$"""{"path":"{{file}}","content":"// {{file}}"}""")
                .EnqueueReport();
        var runner = new PipelineRunner(harness.Services) { ReposOverride = Repos, TicketIdOverride = id };
        runner.RequestContext[ContextKeys.ProjectName] = Project;
        runner.RequestContext[ContextKeys.TrackerPlatform] = "github";
        runner.RequestContext[ContextKeys.ApprovedSpecSet] = SpecApprovalJson.Write(record);
        var result = await runner.RunAsync("code");
        result.IsSuccess.Should().BeTrue(result.Message);
        runner.LastContext!.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress).Specs
            .Should().HaveCount(files.Length).And.OnlyContain(p => p.State == SpecRunState.Done);
    }

    private static async Task LandAsync(RealCompositionHarness harness, RecordingBranchSources branches, string id)
    {
        branches.Gone.Add(TicketBranchNamer.Compose(new TicketId(id)).Value); // cleanup deleted the merged ticket branch
        using var scope = harness.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ITicketLandingStore>().TryRecordAsync(new TicketLandingFact(Tracker,
            TicketKey.For("github", id).Value, id, Project, TicketLandingKinds.Merged, DateTimeOffset.UtcNow, null, null,
            $"https://stub.test/pulls/{id}"), default);
    }

    private static async Task<(IReadOnlyList<SeriesCriterion> Criteria, IReadOnlyList<string> Unread)> CheckAsync(
        RealCompositionHarness harness)
    {
        var record = new SeriesRunRecord("sr-1", "conv-1", Project, "sample.user", true, SeriesRunStates.Running,
            [new SeriesTicket("1", "#1", "Ticket 1", false, DateTimeOffset.UtcNow), new SeriesTicket("2", "#2", "Ticket 2", false, DateTimeOffset.UtcNow)],
            DateTimeOffset.UtcNow) { IntegrationBranch = SeriesBranch };
        using var scope = harness.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISeriesCriteria>().ReadAsync(record, Host, default);
    }
}
