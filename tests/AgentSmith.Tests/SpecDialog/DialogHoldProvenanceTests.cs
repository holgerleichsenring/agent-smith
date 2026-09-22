using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-22-2d11b: what a filed ticket says it was built after. The stamp is taken from the
/// scope the turn actually read through, so a turn that reused a held sandbox must report the
/// sha it landed on after the refresh — never the one the previous turn saw.
/// </summary>
public sealed class DialogHoldProvenanceTests
{
    private readonly SourceScopeHoldFixture _fixture = new();

    [Fact]
    public async Task Dialog_AFiledTicketsProvenance_NamesTheRevisionTheTurnActuallyRead()
    {
        var scopes = _fixture.Scopes(Holds.Live());
        var first = await _fixture.TurnAsync(scopes);
        _fixture.Spawner.Only.RefreshedHead = "sha-after";
        var second = await _fixture.OpenAsync(scopes);

        var stamped = Stamp(scopes, second);

        first.Sha.Should().Be("sha-before");
        var provenance = stamped.Templates.Should().ContainSingle().Subject;
        provenance.Revision.Should().Be("sha-after",
            "the filer runs after the scopes are gone, so the stamp is all it has");
        provenance.Opened.Should().BeTrue();
        await second.DisposeAsync();
    }

    // The stamp the turn takes while its scopes are still open — the shape SpecDialogTurnRunner
    // uses, with the scope under test standing in for a template the analysis read.
    private static OutcomeProposal Stamp(ISourceScopeSandboxFactory scopes, ISourceScopeSandbox scope) =>
        new SpecDialogTemplateScopes(
                new ProjectTemplateScopes(scopes, NullLogger<ProjectTemplateScopes>.Instance),
                NullLogger<SpecDialogTemplateScopes>.Instance)
            .Stamp(
                new AnswerOutcome(),
                SourceScopeHoldFixture.Project,
                new Dictionary<string, ISourceScopeSandbox> { ["template:default"] = scope });
}
