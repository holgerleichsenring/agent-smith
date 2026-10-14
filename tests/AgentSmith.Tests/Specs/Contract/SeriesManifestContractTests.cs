using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Specs.Contract;

/// <summary>2026-10-13-8762j: the series manifest carries the contract, and the run reads it back from the branch.</summary>
public sealed class SeriesManifestContractTests
{
    private const string Key = "azdo-19106";
    private const string Base = "2026-10-06-0a0a";

    [Fact]
    public async Task SeriesManifest_Contract_RoundTripsThroughSeriesReader()
    {
        var contract = SeriesContractFixtures.Of(
            names: [new("SpecReviewRound", "event", 87, Base + "a", [Base + "b"])],
            data: [new("rounds", "RunSpecReviewRounds", Base + "a")],
            areas: [new(Base + "a", ["src/A.cs", "src/Review/"]), new(Base + "b", ["src/A.cs"])],
            budgets: [new("src/A.cs", Base + "a", 100), new("src/A.cs", Base + "b", 110)]);

        var read = await ReadAsync(Set() with { Contract = contract });

        var back = read.Set!.Contract!;
        back.Names.Should().ContainSingle().Which.Writers.Should().Equal(Base + "b");
        (back.Names[0].Name, back.Names[0].Kind, back.Names[0].Number, back.Names[0].Owner)
            .Should().Be(("SpecReviewRound", "event", 87, Base + "a"));
        back.Data.Should().Equal(new SeriesContractDatum("rounds", "RunSpecReviewRounds", Base + "a"));
        back.Areas.Select(a => (a.Spec, string.Join(",", a.Paths))).Should().Equal(
            (Base + "a", "src/A.cs,src/Review/"), (Base + "b", "src/A.cs"));
        back.Budgets.Should().Equal(new SeriesContractBudget("src/A.cs", Base + "a", 100), new SeriesContractBudget("src/A.cs", Base + "b", 110));
    }

    [Fact]
    public void SeriesManifest_NoContract_WritesNoKey()
    {
        new SeriesManifest().Serialize(Set()).Should().NotContain("contract");
    }

    private static SpecSet Set() => new(
        Key, [Spec(Base + "a"), Spec(Base + "b")], SpecAccounting.Empty,
        [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)], SpecSource.Derived, Series: Base);

    private static DerivedSpec Spec(string id) =>
        new(new SpecDraft(id, "g", $"spec: {id}\ngoal: g\ndone:\n  - d\n", []), "slice", string.Empty, []);

    private static async Task<SpecSetOnBranch> ReadAsync(SpecSet set)
    {
        var branch = new SpecBranchFiles { Key = Key };
        branch.SeedSeries(Base, new SeriesManifest().Serialize(set),
            set.Specs.ToDictionary(s => s.FileStem, s => s.Draft.Yaml));
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(1, Guid.Empty, 0, false, 0, null, "branch-sha"));
        var readers = new Mock<ISandboxFileReaderFactory>();
        readers.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(branch);
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["primary"] = sandbox.Object });
        return await SeriesDoubles.Reader(readers.Object).ReadAsync(
            pipeline, new RepoConnection { Name = "primary" }, [], new TicketKey(Key), default);
    }
}
