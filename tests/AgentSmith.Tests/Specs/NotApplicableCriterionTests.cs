using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-08-25-9749: a criterion whose antecedent the BASE disproves is reported not
/// applicable, and is neither outstanding nor a pass on its own.
/// <para>
/// A live two-repository migration failed on two ratified criteria while every build and
/// every integration suite in both repositories exited 0. Both refusals described what the
/// ACCOUNT lacked — "pre-existing transport configurations were not established for every
/// host" — over a base that never carried that transport. The note carried the truth all
/// the way to the operator and changed nothing, because the row was a bool.
/// </para>
/// </summary>
public sealed class NotApplicableCriterionTests
{
    private const string Repo = "Sample.Server";
    private const string BaseRef = "origin/main";
    private const string Criterion =
        "Every topic-transport publisher uses the required subscription shortening, "
        + "where the topic transport was previously configured";
    private const string Antecedent = "a previously configured topic transport";

    private sealed class SearchSandbox(int exitCode) : ISandbox
    {
        public string JobId => "not-applicable";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct) =>
            Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, exitCode, false, 0.1, null, string.Empty));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static BranchSearch Search(int exitCode, bool withBase = true) =>
        new(new Dictionary<string, ISandbox> { [Repo] = new SearchSandbox(exitCode) },
            NullLogger.Instance,
            withBase ? new Dictionary<string, string?> { [Repo] = BaseRef } : null);

    private static AccountRow Row(string? citation, string? antecedent = Antecedent) =>
        new(Criterion, AccountDisposition.NotApplicable, citation, "nothing to apply to",
            Antecedent: antecedent);

    private static CriterionAccount Resolve(BranchSearch search, AccountRow row) =>
        AccountTools.ResolverOver(string.Empty, [], search).Resolve(row);

    [Fact]
    public async Task AccountRow_NotApplicableWithAResolvingBaseCitation_IsAccepted()
    {
        var search = Search(exitCode: 1);
        await search.SearchBase(Repo, "TopicTransport");

        var resolved = Resolve(search, Row("TopicTransport"));

        resolved.Disposition.Should().Be(AccountDisposition.NotApplicable);
        resolved.Antecedent.Should().Be(Antecedent,
            "the answer records what it declared false, so the corpus can be labelled against it");
        resolved.Citation.Should().Contain(BaseRef, "the row carries the base search that disproves it");
    }

    [Fact]
    public async Task AccountRow_NotApplicableCitingABranchSearch_FallsBackToNotSatisfied()
    {
        // A branch search says what is there NOW. A conditional asks what was there BEFORE,
        // and the delivery itself is exactly what stands between the two.
        var search = Search(exitCode: 1);
        await search.SearchBase(Repo, "SomethingElse");
        await search.SearchBranch(Repo, "TopicTransport");

        var resolved = Resolve(search, Row("TopicTransport"));

        resolved.Disposition.Should().Be(AccountDisposition.NotSatisfied);
        resolved.Note.Should().Contain("search of the BASE");
    }

    [Fact]
    public async Task AccountRow_NotApplicableWithoutANamedAntecedent_FallsBackToNotSatisfied()
    {
        var search = Search(exitCode: 1);
        await search.SearchBase(Repo, "TopicTransport");

        var resolved = Resolve(search, Row("TopicTransport", antecedent: null));

        resolved.Disposition.Should().Be(AccountDisposition.NotSatisfied);
        resolved.Note.Should().Contain("without naming the precondition");
    }

    [Fact]
    public async Task AccountRow_NotApplicableWithNoBaseAvailable_FallsBackToNotSatisfied()
    {
        // A repository whose ladder fell through to HEAD has no base: nothing it could
        // search can say what was there before the delivery.
        var search = Search(exitCode: 1, withBase: false);
        await search.SearchBase(Repo, "TopicTransport");

        var resolved = Resolve(search, Row("TopicTransport"));

        resolved.Disposition.Should().Be(AccountDisposition.NotSatisfied);
        resolved.Note.Should().Contain("no search of the base found anything absent");
    }

    /// <summary>A base search that could not RUN (exit above 1) proves nothing, and must not
    /// be usable as the absence it failed to establish.</summary>
    [Fact]
    public async Task AccountRow_NotApplicableCitingABaseSearchThatCouldNotRun_FallsBackToNotSatisfied()
    {
        var search = Search(exitCode: 2);
        await search.SearchBase(Repo, "TopicTransport");

        Resolve(search, Row("TopicTransport")).Disposition
            .Should().Be(AccountDisposition.NotSatisfied);
    }

    /// <summary>A base search that FOUND the antecedent (exit 0) is the opposite proof: the
    /// criterion applies, and the account has to answer it.</summary>
    [Fact]
    public async Task AccountRow_NotApplicableWhereTheBaseCarriesTheAntecedent_FallsBackToNotSatisfied()
    {
        var search = Search(exitCode: 0);
        await search.SearchBase(Repo, "TopicTransport");

        Resolve(search, Row("TopicTransport")).Disposition
            .Should().Be(AccountDisposition.NotSatisfied);
    }

    [Fact]
    public void AccountWindowMerge_SatisfiedInOneWindow_BeatsNotApplicable()
    {
        var merged = AccountWindowMerge.Of(
        [
            [Row("TopicTransport")],
            [new AccountRow(Criterion, AccountDisposition.Satisfied, "src/Sample.cs")],
        ]);

        merged.Should().ContainSingle().Which.Disposition
            .Should().Be(AccountDisposition.Satisfied, "positive evidence is monotone");
    }

    [Fact]
    public void AccountWindowMerge_NotApplicableInOneWindow_BeatsNotSatisfied()
    {
        var merged = AccountWindowMerge.Of(
        [
            [new AccountRow(Criterion, AccountDisposition.NotSatisfied, null, "no file shows it")],
            [Row("TopicTransport")],
        ]);

        merged.Should().ContainSingle().Which.Disposition.Should().Be(
            AccountDisposition.NotApplicable,
            "a proof about the base is not contradicted by one slice failing to find a file");
    }

    [Fact]
    public void AccountWindowMerge_NotApplicableInAnEarlierWindow_IsNotLoweredByALaterOne()
    {
        var merged = AccountWindowMerge.Of(
        [
            [Row("TopicTransport")],
            [new AccountRow(Criterion, AccountDisposition.NotSatisfied, null, "no file shows it")],
        ]);

        merged.Should().ContainSingle().Which.Disposition
            .Should().Be(AccountDisposition.NotApplicable, "the ranking does not depend on window order");
    }

    [Fact]
    public void SpecAccount_ANotApplicableCriterion_IsNotOutstanding()
    {
        var account = new SpecAccount(Repo,
        [
            new CriterionAccount("the hosts build", AccountDisposition.Satisfied, "dotnet build"),
            new CriterionAccount(
                Criterion, AccountDisposition.NotApplicable, "TopicTransport", Antecedent: Antecedent),
        ]);

        account.Outstanding.Should().BeEmpty();
        account.Declined.Should().ContainSingle().Which.Criterion.Should().Be(Criterion);
        account.Delivered.Should().BeTrue("something was proven and nothing is outstanding");
    }

    [Fact]
    public void SpecAccount_EveryCriterionNotApplicable_IsNotDelivered()
    {
        var account = new SpecAccount(Repo,
        [
            new CriterionAccount(
                Criterion, AccountDisposition.NotApplicable, "TopicTransport", Antecedent: Antecedent),
        ]);

        account.Outstanding.Should().BeEmpty();
        account.Delivered.Should().BeFalse("an account that satisfied nothing has proven nothing");
    }

    [Fact]
    public void SpecAccountRenderer_ANotApplicableRow_ReadsAsItsOwnState()
    {
        var markdown = SpecAccountRenderer.ToMarkdown(
        [
            new SpecAccount(Repo,
            [
                new CriterionAccount("the hosts build", AccountDisposition.Satisfied, "dotnet build"),
                new CriterionAccount(
                    Criterion, AccountDisposition.NotApplicable, "TopicTransport", Antecedent: Antecedent),
                new CriterionAccount("the routes are explicit", AccountDisposition.NotSatisfied, Note: "none found"),
            ]),
        ]);

        markdown.Should().Contain("[~] " + Criterion);
        markdown.Should().Contain("not applicable: the base carries no " + Antecedent);
        markdown.Should().Contain("[x] the hosts build");
        markdown.Should().Contain("[ ] the routes are explicit");
    }
}
