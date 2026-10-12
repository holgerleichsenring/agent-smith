using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Models;
using AgentSmith.Tests.SpecDialog;
using FluentAssertions;

namespace AgentSmith.Tests.SpecNaming;

/// <summary>
/// 2026-10-12-967aa: a design-partner reply naming the spec outcome kind the way a later skill
/// release does is refused with the same correction as the phase kind — not as an unknown kind.
/// </summary>
public sealed class SpecNamedOutcomeKindTests
{
    [Theory]
    [InlineData("phase")]
    [InlineData("spec")]
    public void Resolve_AnOutcomeBlockOfTheSpecKind_IsToldToEmitTheBareYaml(string kind)
    {
        var validator = new SpecDraftValidator(new SpecSchemaProvider());
        var reader = new SpecDraftReader();
        var resolver = new OutcomeProposalResolver(
            validator, reader, new BugOutcomeParser(),
            new EpicOutcomeParser(validator, reader, new RequiresEdgeChecker(), DialogTestRepos.Real()),
            DialogTestRepos.Real());

        var resolution = resolver.Resolve($"Here.\n\n```outcome\nkind: {kind}\n```", []);

        resolution.Should().BeOfType<OutcomeInvalid>()
            .Which.Error.Should().Be($"kind '{kind}' needs no ```outcome block — emit the phase spec as the bare ```yaml block");
    }
}
