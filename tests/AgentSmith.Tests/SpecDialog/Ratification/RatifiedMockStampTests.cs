using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Architecture;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;

namespace AgentSmith.Tests.SpecDialog.Ratification;

/// <summary>
/// 2026-10-13-8762s: <c>ratified_mock</c> means a PERSON ratified that version. Filing stamps it from the
/// conversation's ratification turn and removes a value the model wrote; the schema admits a positive
/// integer only; the reader reads it; filing maps each minted id back to the proposal's id.
/// </summary>
public sealed class RatifiedMockStampTests : IDisposable
{
    private readonly RatificationRig _rig = new();

    public void Dispose() => _rig.Dispose();

    [Fact]
    public async Task RatifiedMockStamp_RatifiedInAnotherScopeDuringTheApprovalWait_IsStamped()
    {
        var session = await _rig.OpenAsync();
        await _rig.Mocks.AddAsync(session, "spec-b", "<p>1</p>", default);
        await _rig.Mocks.AddAsync(session, "spec-b", "<p>2</p>", default);
        using var turn = _rig.Provider.CreateScope();
        var state = await RatificationRig.AppendAsync(turn, TranscriptRole.Assistant, "epic", SpecDialogTurnKind.EpicProposal);

        await _rig.RatifyAsync(session, "spec-b", 2);
        var stamped = (EpicOutcome)await turn.ServiceProvider.GetRequiredService<RatifiedMockStamp>().StampAsync(
            state!, new EpicOutcome(Draft("parent"), [Draft("spec-a"), Draft("spec-b")]), CancellationToken.None);

        stamped.Children.Select(c => c.RatifiedMock).Should().Equal(null, 2);
        stamped.Children[1].Yaml.Should().EndWith("\nratified_mock: 2");
        new SpecDraftReader().Read(stamped.Children[1].Yaml).RatifiedMock.Should().Be(2);
    }

    [Fact]
    public void RatifiedMockStamp_ModelWrittenValueWithoutRatification_IsRemoved()
    {
        var written = Draft("spec-a", "ratified_mock: 4\ndone:\n  - it works") with { RatifiedMock = 4 };

        var stamped = RatifiedMockStamp.Stamp(written, new Dictionary<string, int>());
        var superseded = RatifiedMockStamp.Stamp(written, new Dictionary<string, int> { ["spec-a"] = 1 });

        stamped.RatifiedMock.Should().BeNull();
        stamped.Yaml.Should().NotContain("ratified_mock").And.Contain("done:\n  - it works");
        superseded.Yaml.Should().Contain("ratified_mock: 1").And.NotContain("ratified_mock: 4");
    }

    [Fact]
    public void SpecDraftReader_RatifiedMock_IsRead()
    {
        new SpecDraftReader().Read("spec: s1\ngoal: g\nratified_mock: 3").RatifiedMock.Should().Be(3);
        new SpecDraftReader().Read("spec: s1\ngoal: g").RatifiedMock.Should().BeNull();
    }

    [Theory]
    [InlineData("2", true)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("1.5", false)]
    [InlineData("\"2\"", false)]
    public void SpecSchema_RatifiedMock_AcceptsPositiveIntegerOnly(string value, bool valid)
    {
        var document = JsonNode.Parse($$"""{"spec": "2026-10-13-8762s", "goal": "g", "ratified_mock": {{value}}}""")!;

        SpecSchemaFile.Validate(document).Should().HaveCount(valid ? 0 : 1);
    }

    [Fact]
    public void FiledSeriesFactory_Sources_MapMintedIdsToProposalIds()
    {
        var factory = new FiledSeriesFactory(new SeriesIdFactory(TimeProvider.System),
            new SeriesDraftIds(new SpecDraftIdRewriter()), NullLogger<FiledSeriesFactory>.Instance);
        var drafts = new[] { Draft("p-first"), Draft("p-second") };

        var filed = factory.Under("2026-10-14-ab12", drafts, new EpicOutcome(Draft("parent"), drafts));

        filed.Sources.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["2026-10-14-ab12a"] = "p-first",
            ["2026-10-14-ab12b"] = "p-second",
        });
    }

    [Fact]
    public void DerivedSpecBuilder_ModelElementNamesRatifiedMock_DraftCarriesNone()
    {
        var builder = new DerivedSpecBuilder(new SpecDraftValidator(
                new AgentSmith.Application.Services.Validation.SpecSchemaProvider()), new SpecDraftReader(),
            new DerivedSpecYamlRenderer(), new SpecDerivationEnvelope(), new FactResolver());
        using var element = System.Text.Json.JsonDocument.Parse(
            """{"goal": "Build the page.", "done": ["the page renders"], "ratified_mock": 2}""");

        var (spec, error) = builder.Build(element.RootElement, 0, "2026-10-14-ab12a", "1", [], [], null);

        error.Should().BeNull();
        spec!.Draft.RatifiedMock.Should().BeNull("a run's derivation renders named fields only");
        spec.Draft.Yaml.Should().NotContain("ratified_mock");
    }

    private static SpecDraft Draft(string id, string? extra = null) => TranscriptReloadTests.Draft(id, extra);
}
