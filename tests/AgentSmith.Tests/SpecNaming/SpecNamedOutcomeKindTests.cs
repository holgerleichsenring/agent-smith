using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Models;
using AgentSmith.Tests.SpecDialog;
using FluentAssertions;

namespace AgentSmith.Tests.SpecNaming;

/// <summary>
/// 2026-10-12-967aa/967ah: a design-partner reply naming the spec outcome kind is told to emit the
/// bare yaml; the kind's old name is an unknown kind like any other word.
/// </summary>
public sealed class SpecNamedOutcomeKindTests
{
    [Fact]
    public void Resolve_AnOutcomeBlockOfTheSpecKind_IsToldToEmitTheBareYaml() =>
        Resolve("spec").Should().BeOfType<OutcomeInvalid>()
            .Which.Error.Should().Be("kind 'spec' needs no ```outcome block — emit the spec as the bare ```yaml block");

    [Fact]
    public void Resolve_AnOutcomeBlockOfTheOldKind_IsAnUnknownKind() =>
        Resolve("phase").Should().BeOfType<OutcomeInvalid>()
            .Which.Error.Should().Be("unknown outcome kind 'phase' — expected bug, epic or tickets");

    private static OutcomeResolution Resolve(string kind)
    {
        var validator = new SpecDraftValidator(new SpecSchemaProvider());
        var reader = new SpecDraftReader();
        var resolver = new OutcomeProposalResolver(
            validator, reader, new BugOutcomeParser(),
            new EpicOutcomeParser(validator, reader, new RequiresEdgeChecker(), DialogTestRepos.Real(), AgentSmith.Tests.TestSupport.SeriesContracts.Reader(), AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader()),
            new TicketGraphOutcomeParser(new TicketGraphChecker(new TicketGraphOrderer())),
            DialogTestRepos.Real());

        return resolver.Resolve($"Here.\n\n```outcome\nkind: {kind}\n```", []);
    }
}
