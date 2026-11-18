using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>2026-11-18-3863g: a proposal that adds a service to a project whose other services are deployable ends with
/// the suggestion to bind it and make it deployable; nothing is guessed.</summary>
public sealed class DeployableSuggestionTests
{
    private static readonly IReadOnlyList<ContextDocument> Grounded =
        [new("app", "api", null, ".agentsmith/contexts/api/context.yaml", ""), new("app", "web", null, ".agentsmith/contexts/web/context.yaml", "")];

    [Fact]
    public void Of_NewContextBesideDeployableSiblings_NamesItTheSiblingsTemplateAndTheTwoSteps()
    {
        var line = DeployableSuggestion.Of(Proposal("billing", "api"), Project(), Grounded);

        line.Should().Contain("`billing` is a new context without a deployment template")
            .And.Contain("`api` is delivered after `k8s-gitops` of `delivery`")
            .And.Contain("bind `billing` to that template").And.Contain("cut a spec that makes it deployable");
    }

    [Fact]
    public void Of_ExistingOrBoundContext_NoDeploymentBinding_OrNoGrounding_GiveNoLine()
    {
        DeployableSuggestion.Of(Proposal("web", "api"), Project(), Grounded).Should().BeNull("web exists already");
        DeployableSuggestion.Of(Proposal("billing"), new ResolvedProject { Name = "shop" }, Grounded).Should().BeNull();
        DeployableSuggestion.Of(Proposal("billing"), Project(), null).Should().BeNull("what exists is not guessed");
        DeployableSuggestion.Of(new AnswerOutcome(), Project(), Grounded).Should().BeNull();
    }

    [Fact]
    public async Task CollectReply_ProposalAddingAService_EndsItsReplyWithTheSuggestion()
    {
        var pipeline = new PipelineContext();
        var slot = new SpecDialogReplySlot();
        pipeline.Set(ContextKeys.SpecDialogReplySlot, slot);
        pipeline.Set(ContextKeys.MasterAnswer, "Here is the series.");
        pipeline.Set(ContextKeys.SpecDialogOutcome, Proposal("billing"));
        pipeline.Set(ContextKeys.SpecDialogProject, Project());
        pipeline.Set(ContextKeys.RepoContextYamls, Grounded);

        var result = await new CollectSpecDialogReplyHandler().ExecuteAsync(new CollectSpecDialogReplyContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        slot.Reply.Should().StartWith("Here is the series.\n\nDeployable: `billing`");
    }

    private static OutcomeProposal Proposal(params string[] contexts) =>
        new SpecOutcome(new SpecDraft("2026-11-18-0001a", "Add billing", "spec: x", []) { Contexts = contexts });

    private static ResolvedProject Project() => new()
    {
        Name = "shop",
        Templates =
        [
            new ProjectTemplate("api", "k8s-gitops", "v1.0.0", new RepoConnection { Name = "delivery", Url = "https://stub.test/delivery" },
                null, TemplateKinds.Deployment, [new DeploymentEnvironment("test", null, false, new Dictionary<string, string>())]),
        ],
    };
}
