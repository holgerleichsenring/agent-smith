using System.Text.Json;
using System.Text.RegularExpressions;
using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0507: the id rule itself — which shapes a phase spec and its decision file may
/// declare, stated identically in this repository and in the plugin templates.
/// <para>
/// The counter namespace is CLOSED, not retired: every shipped id stays valid and none is
/// renamed. What is pinned here is that widening for the date-minted shape did not
/// quietly admit a THIRD shape that no reader agrees on.
/// </para>
/// </summary>
public sealed class PhaseIdSchemaTests
{
    private const string Minted = "2026-08-24-8a3f";

    [Fact]
    public void Schema_CounterShapedId_StillValidates()
    {
        SpecPattern().IsMatch("p0507").Should().BeTrue("the closed namespace stays valid forever");
        SpecPattern().IsMatch("p0057a").Should().BeTrue();
        SpecPattern().IsMatch("p0131c-pre").Should().BeTrue();
    }

    [Fact]
    public void Schema_DateMintedId_Validates()
    {
        SpecPattern().IsMatch(Minted).Should().BeTrue();
        SpecPattern().IsMatch($"{Minted}-a-phase-id-can-be-minted-offline").Should().BeTrue(
            "the fixed-width suffix marks where the id ends and the slug begins");
    }

    /// <summary>
    /// The bound is six digits, not four. An id minted from a ticket number lives in the
    /// counter namespace, and a four-digit bound would make the DEPLOYED server reject
    /// every spec derived from a five- or six-digit ticket — the same truncation p0509
    /// was written to stop.
    /// </summary>
    [Fact]
    public void Schema_TicketMintedCounterId_StillValidates()
    {
        foreach (var ticket in new[] { "19106", "482913", "57" })
        {
            var id = PhaseIdFactory.For(ticket, 0);
            SpecPattern().IsMatch(id).Should().BeTrue(
                $"PhaseIdFactory mints '{id}' and SpecDraftValidator checks it against this schema");
        }
    }

    [Fact]
    public void Schema_YearPrefixedCounterId_IsRejected() =>
        SpecPattern().IsMatch("p20260822a").Should().BeFalse(
            "an eight-digit counter id is neither namespace — accepting it would leave a "
            + "third shape valid that no reader agrees on");

    /// <summary>
    /// Both copies THIS repository owns state one identical rule, pinned as a literal so
    /// a one-sided edit goes red.
    /// <para>
    /// The plugin templates are deliberately NOT read here. They live in a separate
    /// repository on its own release cycle: reading its working tree would go red for
    /// anyone who has not cloned it and be green-but-meaningless in CI. Keeping the two
    /// repositories byte-identical is therefore a convention enforced by nothing — which
    /// is exactly how the plugin's copy drifted to <c>^p\d+[a-z]?$</c> unnoticed. The
    /// literal below is what a plugin release must be diffed against, by hand.
    /// </para>
    /// </summary>
    [Fact]
    public void Schema_BothSchemasInThisRepository_StateOneIdenticalRule()
    {
        const string canonical =
            @"^(?:p\d{4,6}[a-z]?|\d{4}-\d{2}-\d{2}-[0-9a-f]{4})(?:-[a-z][a-z0-9-]*)?$";

        SpecPattern().ToString().Should().Be(canonical);
        PatternIn(RepoSchema("decision.schema.json")).Should().Be(canonical,
            "a spec id and the decision file that records it are the same id");
    }

    internal static Regex SpecPattern() => new(PatternIn(RepoSchema("phase-spec.schema.json")));

    private static string PatternIn(string schemaPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(schemaPath));
        return doc.RootElement.GetProperty("properties").GetProperty("phase")
            .GetProperty("pattern").GetString()!;
    }

    private static string RepoSchema(string name) =>
        Path.Combine(ArchitectureSources.AgentSmithRoot, name);

}
