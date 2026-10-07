using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0509: the factory that names every phase file a run writes into a target repository.
/// 2026-10-06-03c7c: ids are no longer derived from the ticket number — a series' base is
/// minted (<see cref="SeriesIdFactoryTests"/>) — so what is left here is the slug.
/// </summary>
public sealed class PhaseIdFactoryTests
{
    [Fact]
    public void PhaseIdFactory_Slug_IsLowercaseDashedAndBounded()
    {
        PhaseIdFactory.Slug("Rename the Clients!").Should().Be("rename-the-clients");
        PhaseIdFactory.Slug(new string('a', 80)).Length.Should().Be(PhaseIdFactory.MaxSlugLength);
        PhaseIdFactory.Slug("!!!").Should().Be("phase");
    }

    [Fact]
    public void SeriesMember_SecondPhaseOfASeries_GetsTheNextLetter()
    {
        SeriesIdFactory.Member("2026-10-06-03c7", 0).Should().Be("2026-10-06-03c7a");
        SeriesIdFactory.Member("2026-10-06-03c7", 1).Should().Be("2026-10-06-03c7b");
        SeriesIdFactory.Member("2026-10-06-03c7", 2).Should().Be("2026-10-06-03c7c");
    }
}
