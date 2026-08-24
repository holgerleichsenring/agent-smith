using System.Text.RegularExpressions;
using AgentSmith.Tests.Prompts;
using FluentAssertions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// p0507: the design partner writes the first draft of a spec, so the placeholder it
/// shows is the shape an operator copies. Until this phase it showed <c>pNNNN</c>, which
/// matches NEITHER id pattern — the master was teaching a shape the schema rejects.
/// <para>
/// Armed at the pin, the way p0504 armed its packaging assertion: the master only carries
/// the new wording once a catalog release ships it, so below the pin this asserts the
/// packaged master is still readable and states what is outstanding.
/// </para>
/// </summary>
public sealed class DesignPartnerPlaceholderTests
{
    private static readonly Version MintedPlaceholderFrom = new(4, 7, 0);

    [Fact]
    public void PackagedDesignPartner_PlaceholderMatchesAValidatingShape()
    {
        var master = PackagedMaster.Read("design-partner-master");

        if (PackagedMaster.Pin < MintedPlaceholderFrom)
        {
            master.Should().NotBeNullOrWhiteSpace(
                "the pinned catalog must still be readable while it predates the new placeholder");
            return;
        }

        master.Should().NotMatchRegex(
            @"\bpNNNN",
            "pNNNN matches neither id pattern, so a draft copying it is rejected by the schema");

        PlaceholderIds(master).Should().NotBeEmpty(
            "the master must still show an example id for an operator to copy")
            .And.OnlyContain(id => SpecId().IsMatch(id),
                "every example the master shows must be a shape the schema accepts");
    }

    private static IReadOnlyList<string> PlaceholderIds(string master) =>
        Regex.Matches(master, @"\b\d{4}-\d{2}-\d{2}-[0-9a-f]{4}\b")
             .Select(m => m.Value)
             .Distinct(StringComparer.Ordinal)
             .ToList();

    private static Regex SpecId() =>
        new(@"^(?:p\d{4,6}[a-z]?|\d{4}-\d{2}-\d{2}-[0-9a-f]{4})(?:-[a-z][a-z0-9-]*)?$");
}
