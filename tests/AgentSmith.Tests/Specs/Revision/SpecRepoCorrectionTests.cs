using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Specs.Review;
using FluentAssertions;
using Moq;
using static AgentSmith.Tests.Specs.Revision.SpecRepoCorrectionRig;

namespace AgentSmith.Tests.Specs.Revision;

/// <summary>
/// 2026-11-27-6625: the spec a coding master wrote names the wrong repository. While the spec is not done, revise_spec
/// moves it: the repository joins the workspace through ensure_repo_sandbox, the spec is placed there, and the write
/// guard follows the revised spec.
/// </summary>
public sealed class SpecRepoCorrectionTests
{
    private const string Reason = "the ticket names billing, not shop";

    private static string MovedYaml => CutRevisionFixtures.With(
        "applies_to: \"backend (export)\"", "applies_to: \"billing (export)\"\nrepos: [billing]");

    [Fact]
    public async Task Revise_SelfWrittenSpecNamesTheWrongRepository_MovesItAndItsWritesPass()
    {
        var rig = Rig();
        var asked = new List<string>();
        (await Write(rig, "billing/src/Invoice.cs")).Should().StartWith("Error:", "the spec still names shop");

        var reply = await rig.ReviseAsync(MovedYaml, Reason, Joining(rig.Pipeline, asked));

        reply.Should().Contain("revision 2").And.Contain("written into billing now").And.Contain("What shop already holds");
        asked.Should().Equal("billing");
        rig.Current.Repos.Should().Equal("billing");
        var published = rig.Loop.Published.Should().ContainSingle().Subject;
        var placed = new SeriesRepos().Place(published, new SeriesScope(
            rig.Pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos), "shop", [], "shop"));
        placed.Carrier!.Name.Should().Be("billing", "the spec is written into the repository it now names");
        (await Write(rig, "billing/src/Invoice.cs")).Should().Be("Wrote");
        (await Write(rig, "shop/src/Invoice.cs")).Should().StartWith("Error:").And.Contain("written into billing alone");
    }

    [Fact]
    public async Task Revise_ANewRepositoryThatCannotJoin_WritesNothing()
    {
        var rig = Rig();

        var reply = await rig.ReviseAsync(MovedYaml, Reason,
            (_, _) => Task.FromResult("no capacity right now — continue without this repo"));

        reply.Should().StartWith("Not revised").And.Contain("'billing' could not join").And.Contain("no capacity");
        rig.Loop.Published.Should().BeEmpty();
        rig.Current.Repos.Should().Equal("shop");
    }

    [Fact]
    public async Task Revise_ToTwoRepositories_IsRefusedBeforeAnyJoins()
    {
        var rig = Rig();
        var asked = new List<string>();

        var reply = await rig.ReviseAsync(MovedYaml.Replace("repos: [billing]", "repos: [billing, ledger]"), Reason,
            Joining(rig.Pipeline, asked));

        reply.Should().StartWith("Not revised").And.Contain("exactly one repository");
        asked.Should().BeEmpty();
    }

    [Fact]
    public async Task Revise_MovedButTheBranchRefusedTheWrite_PutsTheCarrierBack()
    {
        var rig = Rig();
        rig.Pipeline.Set(ContextKeys.SpecRepo, "shop");
        rig.Loop.Publisher.Setup(p => p.PublishAsync(It.IsAny<PipelineContext>(), It.IsAny<string>(), It.IsAny<SeriesScope>(),
                It.IsAny<SpecSet>(), It.IsAny<IReadOnlyList<IgnoredInstruction>>(), It.IsAny<CancellationToken>()))
            .Callback((PipelineContext pipeline, string _, SeriesScope _, SpecSet _, IReadOnlyList<IgnoredInstruction> _,
                CancellationToken _) => pipeline.Set(ContextKeys.SpecRepo, "billing"))
            .ReturnsAsync(CommandResult.Fail("push rejected"));

        var reply = await rig.ReviseAsync(MovedYaml, Reason, Joining(rig.Pipeline, []));

        reply.Should().StartWith("Not revised").And.Contain("push rejected");
        rig.Pipeline.Get<string>(ContextKeys.SpecRepo).Should().Be("shop");
        rig.Current.Repos.Should().Equal("shop");
    }

    [Fact]
    public async Task Revise_AnExecutedSpecsRepos_IsRefused()
    {
        var rig = Rig();
        var set = rig.Pipeline.Get<SpecSet>(ContextKeys.SpecSet);
        rig.Pipeline.Set(ContextKeys.SpecSet, set with { Executed = [rig.Current.SpecId] });
        var asked = new List<string>();

        var reply = await rig.ReviseAsync(MovedYaml, Reason, Joining(rig.Pipeline, asked));

        reply.Should().StartWith("Not revised").And.Contain("repos and applies_to");
        asked.Should().BeEmpty();
        rig.Loop.Published.Should().BeEmpty();
    }
}
