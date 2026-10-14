using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.Architecture;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>
/// 2026-10-13-8762n: entering a spec re-grounds it — one revision when a citation moved, nothing when none
/// did, and on a revision the branch did not take the spec running on its original text, the moves stale.
/// SelectSpec does it before the spec's start heads, so no spec's own diff carries the revision. Real git.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class SpecEntryGroundingTests
{
    private const string Code = "src/Api/Orders.cs";
    private const string Second = "2026-10-14-1111b";
    private static readonly SpecApproval Approval = new(CitationRepo.At(2).AddMinutes(30), "conversation-1", "ada");

    /// <summary>Orders.cs, the spec citing its lines 2-3 filed after it, then two lines put on top of them.</summary>
    private static async Task<(PipelineContext Pipeline, SpecSet Set, DerivedSpec Spec)> MovedAsync(CitationRepo repo, bool move = true)
    {
        await repo.WriteAsync(Code, CitationRepo.Numbered("order", 8));
        await repo.CommitAsync("code", CitationRepo.At(1));
        var yaml = CitationRepo.SpecYaml(CitationRepo.SpecId, Code + ":2-3");
        await repo.WriteAsync(CitationRepo.SpecPath(), yaml);
        await repo.CommitAsync("filed", CitationRepo.At(2));
        if (move) await repo.WriteAsync(Code, "// one\n// two\n" + CitationRepo.Numbered("order", 8));
        await repo.CommitAsync("a predecessor put two lines on top", CitationRepo.At(3));
        var spec = CitationRepo.Spec(yaml);
        var set = CitationRepo.Set(Approval, spec);
        var pipeline = CitationRepo.Pipeline(repo);
        pipeline.Set(ContextKeys.SpecSet, set);
        return (pipeline, set, spec);
    }

    [Fact]
    public async Task SpecEntryGrounding_MovedCitation_OneRevisionWithCause_ApprovalKept()
    {
        await using var repo = await CitationRepo.CreateAsync();
        if (repo is null) return;
        var (pipeline, set, spec) = await MovedAsync(repo);
        var publisher = new GitSpecSetPublisher(repo, CitationRepo.At(4));

        var ground = await publisher.Grounding().GroundAsync(pipeline, set, spec, CancellationToken.None);

        var published = publisher.Published.Should().ContainSingle().Subject;
        published.Revisions[^1].Cause.Should().Be(
            $"{SpecRevisionCause.CitationsRegrounded} {CitationRepo.SpecId}: {Code}:2-3 -> {Code}:4-5");
        published.Approval.Should().Be(Approval);
        ground.Spec.Draft.Yaml.Should().Contain($"evidence: \"{Code}:4-5\"");
        ground.StepNote.Should().BeEmpty();
        pipeline.Get<IReadOnlyList<string>>(ContextKeys.StaleCitations).Should().BeEmpty();
    }

    [Fact]
    public async Task SpecEntryGrounding_NothingMoved_PublishesNothing()
    {
        await using var repo = await CitationRepo.CreateAsync();
        if (repo is null) return;
        var (pipeline, set, spec) = await MovedAsync(repo, move: false);
        var publisher = new GitSpecSetPublisher(repo, CitationRepo.At(4));

        var ground = await publisher.Grounding().GroundAsync(pipeline, set, spec, CancellationToken.None);

        publisher.Published.Should().BeEmpty();
        ground.Spec.Should().BeSameAs(spec);
        ground.StepNote.Should().BeEmpty();
    }

    [Fact]
    public async Task SpecEntryGrounding_PublishFails_OriginalSpecAndSetKept_MovedCitationsListedStale()
    {
        await using var repo = await CitationRepo.CreateAsync();
        if (repo is null) return;
        var (pipeline, set, spec) = await MovedAsync(repo);
        var publisher = new GitSpecSetPublisher(repo, CitationRepo.At(4)) { Fails = true };

        var ground = await publisher.Grounding().GroundAsync(pipeline, set, spec, CancellationToken.None);

        ground.Spec.Should().BeSameAs(spec, "the spec runs on the text the branch states");
        pipeline.Get<SpecSet>(ContextKeys.SpecSet).Should().BeSameAs(set, "the set it was handed is put back");
        pipeline.Get<IReadOnlyList<string>>(ContextKeys.StaleCitations).Should().ContainSingle()
            .Which.Should().Be($"{Code}:2-3: its text is now at {Code}:4-5; the re-ground revision was not published");
        ground.StepNote.Should().Contain("1 stale citation(s)").And.Contain("the re-ground revision was not published: ")
            .And.Contain("the push was refused");
    }

    [Fact]
    public async Task SelectSpec_MovedCitation_CurrentSpecIsGroundedAndPublishedBeforeStartHeads()
    {
        await using var repo = await CitationRepo.CreateAsync();
        if (repo is null) return;
        var (pipeline, _, _) = await MovedAsync(repo);
        var publisher = new GitSpecSetPublisher(repo, CitationRepo.At(4));

        var result = await publisher.Select().ExecuteAsync(new SelectSpecContext(CitationRepo.SpecId, pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<SpecDraft>(ContextKeys.CurrentSpec).Yaml.Should().Contain($"{Code}:4-5");
        var head = (await repo.GitAsync(null, "rev-parse", "HEAD")).Trim();
        pipeline.Get<string>(ContextKeys.SpecRevisionSha).Should().Be(head);
        SpecStartHeads.For(pipeline, repo.Name).Should().Be(head, "the revision precedes the spec's start head");
    }

    [Fact]
    public async Task SelectSpec_GroundingPublishFails_StepSucceedsAndNamesIt()
    {
        await using var repo = await CitationRepo.CreateAsync();
        if (repo is null) return;
        var (pipeline, _, spec) = await MovedAsync(repo);
        var publisher = new GitSpecSetPublisher(repo, CitationRepo.At(4)) { Fails = true };

        var result = await publisher.Select().ExecuteAsync(new SelectSpecContext(CitationRepo.SpecId, pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("a re-ground is optional; its failed publish never fails the spec");
        result.Message.Should().StartWith($"Spec {CitationRepo.SpecId}: ").And.Contain("the re-ground revision was not published");
        pipeline.Get<SpecDraft>(ContextKeys.CurrentSpec).Yaml.Should().Be(spec.Draft.Yaml);
    }

    /// <summary>
    /// The end-to-end the spec names a harness test: spec 1 moves the lines spec 2 cites, spec 2 enters
    /// re-grounded and its own diff carries no spec path. The PipelineHarness's stub sandbox answers every git
    /// step with exit 0 and no output, so the chain runs here over a real clone (decision DEVIATION in 8762n).
    /// </summary>
    [Fact]
    public async Task Harness_TwoSpecSeries_SpecOneMovesLinesSpecTwoCites_SpecTwoEntersRegroundedAndItsDiffCarriesNoSpecsPath()
    {
        await using var repo = await CitationRepo.CreateAsync();
        if (repo is null) return;
        await repo.WriteAsync(Code, CitationRepo.Numbered("order", 8));
        await repo.CommitAsync("code", CitationRepo.At(1));
        var first = CitationRepo.Spec(CitationRepo.SpecYaml(CitationRepo.SpecId, "README.md"));
        var second = CitationRepo.Spec(CitationRepo.SpecYaml(Second, Code + ":2-3"));
        await repo.WriteAsync(CitationRepo.SpecPath(), first.Draft.Yaml + "\n");
        await repo.WriteAsync(CitationRepo.SpecPath(Second), second.Draft.Yaml + "\n");
        await repo.CommitAsync("the series is filed", CitationRepo.At(2));
        var pipeline = CitationRepo.Pipeline(repo);
        pipeline.Set(ContextKeys.SpecSet, CitationRepo.Set(null, first, second));
        var select = new GitSpecSetPublisher(repo, CitationRepo.At(4)).Select();

        (await select.ExecuteAsync(new SelectSpecContext(CitationRepo.SpecId, pipeline), CancellationToken.None)).IsSuccess.Should().BeTrue();
        await repo.WriteAsync(Code, "// one\n// two\n" + CitationRepo.Numbered("order", 8));
        await repo.CommitAsync("spec one moves the lines", CitationRepo.At(3));
        (await select.ExecuteAsync(new SelectSpecContext(Second, pipeline), CancellationToken.None)).IsSuccess.Should().BeTrue();
        await repo.WriteAsync("src/Api/Callers.cs", "// spec two's own work\n");
        await repo.CommitAsync("spec two's work", CitationRepo.At(5));

        pipeline.Get<SpecDraft>(ContextKeys.CurrentSpec).Yaml.Should().Contain($"{Code}:4-5");
        var diffs = await new SpecDiffs(TestHelpers.TestGit.Delivery, Microsoft.Extensions.Logging.Abstractions.NullLogger<SpecDiffs>.Instance)
            .TakeAsync(pipeline, new Dictionary<string, Contracts.Sandbox.ISandbox> { [repo.Name] = repo.Sandbox }, CancellationToken.None);
        diffs.Single().Text.Should().Contain("src/Api/Callers.cs").And.NotContain(".agentsmith/specs");
    }
}
