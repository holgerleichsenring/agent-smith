using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-c47ea fast-tier end-to-end: on an APPROVED set, a false premise that breaks no done criterion does not
/// stop its spec — the spec is built and delivered, and its master is told the corrected fact. The set comes from the
/// approval record the launcher carries, as a filed ticket's run reads it.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class PremiseCorrectionSpecBlockTests
{
    private const string Series = "2026-11-17-c0de";
    private const string Premise = "every user foreign key is declared without ON DELETE";

    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"done","acceptance":[{"criterion":"Users are deleted only after their rows are.","status":"met","evidence":"restrict"}]}""";

    [Fact]
    public async Task Harness_ApprovedSpecWithAFalsePremise_IsBuiltAndItsMasterIsToldTheCorrection()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(tickets));
        });
        harness.Services.GetRequiredService<HarnessSpecPremiseChecker>()
            .FindsOnCall(1, new PremiseFinding(Premise, "no-longer-holds",
                "fk_teams_conv_user cascades on delete", "M5",
                "[M5] primary: the premise check ran 'read sql/schema.sql' exited 0", "primary: read sql/schema.sql"));
        harness.ChatClient
            .EnqueueText("Planning: map the users.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Users.cs","content":"// users"}""")
            .EnqueueText(GreenVerdict);
        var runner = new PipelineRunner(harness.Services) { DoneStatus = "done", FailedStatus = "failed" };
        runner.RequestContext[ContextKeys.TrackerPlatform] = "github"; // the record's key, as the launcher spells it
        runner.RequestContext[ContextKeys.ApprovedSpecSet] = SpecApprovalJson.Write(Record());

        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue("an approved spec is built on the corrected fact");
        var row = runner.LastContext!.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress).Specs.Single();
        row.State.Should().NotBe(SpecRunState.HandedBack);
        harness.ChatClient.PromptsSeen.Should().Contain(p => p.Contains("Corrected premises") && p.Contains(Premise)
            && p.Contains("fk_teams_conv_user cascades on delete") && p.Contains("[M5]"), "the master builds on what the look showed");
        tickets.Commented.Select(c => c.Comment).Should().NotContain(c => c.Contains(
            Application.Services.Specs.PremiseHandback.Heading), "nothing was handed back");
    }

    private static SpecApprovalRecord Record()
    {
        var id = Series + "a";
        var yaml = $"""
            spec: {id}
            goal: "Persist the users with restrict-only foreign keys"
            facts:
              - claim: "{Premise}"
                evidence: "sql/schema.sql:1"
            steps:
              - id: users
                action: "Map the users"
            done:
              - "Users are deleted only after their rows are."
            """;
        var draft = new SpecDraft(id, "Persist the users with restrict-only foreign keys", yaml, [])
        {
            Done = ["Users are deleted only after their rows are."],
            Steps = [new SpecStep("users", "Map the users", null)],
            Facts = [new SpecFact(Premise, "sql/schema.sql:1")],
        };
        var set = new SpecSet("github-1", [new DerivedSpec(draft, "persist-users", string.Empty, [1])], SpecAccounting.Empty, [],
            SpecSource.Approved, Approval: new SpecApproval(DateTimeOffset.UtcNow, "conversation-1", "sample.user"), Series: Series);
        return new SpecApprovalRecord("github-1", set, []);
    }
}
