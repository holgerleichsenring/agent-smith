using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0521: the repository's own phases are measured against the schema it ships, and the
/// schema's limits stay what the deployed product reads.
/// <para>
/// The schema was enforced on DRAFTS and never on the record: nothing validated
/// <c>.agentsmith/phases/**</c>, so 76 committed files failed the schema they carry and
/// no test noticed — 73 writing decisions in a shape the schema forbids, two over the
/// goal limit, and one that is not parseable YAML at all.
/// </para>
/// <para>
/// The limits themselves are a PRODUCT CONTRACT. This file is an embedded resource the
/// deployed server evaluates on every model-authored draft, and the single-phase fallback
/// THROWS on an invalid document — it has no second chance. Narrowing the goal limit to a
/// house style would crash that path in a customer's installation for any ticket with a
/// long enough title. The repository's convention lives in <see cref="PhaseNameRuleTests"/>.
/// </para>
/// </summary>
public sealed class PhaseSpecFileSchemaTests
{
    [Fact]
    public void PhaseSpec_EveryFile_ValidatesAgainstTheSchema()
    {
        var failures = PhaseSpecFile.All()
            .SelectMany(Failures)
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToList();

        failures.Should().BeEmpty(
            "every phase file is measured against the schema this repository ships.\n  "
            + string.Join("\n  ", failures));
    }

    /// <summary>
    /// The guard on the product contract. 2000 is not this repository's taste — it is what
    /// the deployed validator allows, and the number a customer's ticket title is measured
    /// against. The repo's own 200-character convention is enforced elsewhere, on this
    /// repository's phases only.
    /// </summary>
    [Fact]
    public void PhaseSchema_TheEmbeddedLimit_IsUnchanged() =>
        PhaseSpecSchemaFile.GoalMaxLength.Should().Be(2000,
            "SpecFallback throws on an invalid document, so a narrower limit here turns a "
            + "long ticket title into a crash in a customer's installation");

    /// <summary>
    /// The contract exercised end to end rather than asserted: a ticket title far past any
    /// house style still produces a spec the fallback accepts.
    /// </summary>
    [Fact]
    public void PhaseSchema_ALongTicketTitle_StillProducesAValidFallbackSpec()
    {
        var title = string.Join(' ', Enumerable.Repeat("migrate the reporting client", 30));
        var validator = new SpecDraftValidator(new PhaseSpecSchemaProvider());
        var fallback = new SpecFallback(
            validator, new PhaseDraftReader(), new DerivedPhaseYamlRenderer());

        var build = () => fallback.Build(
            "run-1",
            new Ticket(new TicketId("19106"), title, "Body.", null, "open", "azdo", []),
            [new TicketSegment(0, "Body.", 0, 0)],
            [],
            SpecSource.Derived);

        title.Length.Should().BeGreaterThan(800, "the point is a title no convention allows");
        build.Should().NotThrow(
            "the single-phase fallback has no second chance — an invalid document there is "
            + "an unrecoverable crash, not a re-prompt");
    }

    /// <summary>
    /// A goal over the schema's own limit is the one failure the ratchet carries, because
    /// both offenders are finished phases that are not edited.
    /// </summary>
    private static IEnumerable<string> Failures(PhaseSpecFile file)
    {
        if (file.Document is null)
            return [$"{file.PhaseId}: not parseable YAML"];

        return PhaseSpecSchemaFile.Validate(file.Document)
            .Where(error => !IsBaselinedGoalLength(file, error))
            .Select(error => $"{file.PhaseId}: {error}");
    }

    private static bool IsBaselinedGoalLength(PhaseSpecFile file, string error) =>
        error.StartsWith("/goal:", StringComparison.Ordinal)
        && PhaseNameBaseline.Exempts(PhaseNameBaseline.SchemaGoalLength, file.PhaseId);
}
