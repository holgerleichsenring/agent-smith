using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0509: the factory that names every spec file a run writes into a target repository.
/// 2026-10-06-03c7c: ids are no longer derived from the ticket number — a series' base is
/// minted (<see cref="SeriesIdFactoryTests"/>) — so what is left here is the slug.
/// </summary>
public sealed class SpecIdFactoryTests
{
    [Fact]
    public void SpecIdFactory_Slug_IsLowercaseDashedAndBounded()
    {
        SpecIdFactory.Slug("Rename the Clients!").Should().Be("rename-the-clients");
        SpecIdFactory.Slug(new string('a', 80)).Length.Should().Be(SpecIdFactory.MaxSlugLength);
        SpecIdFactory.Slug("!!!").Should().Be("spec");
    }

    [Fact]
    public void SeriesMember_SecondSpecOfASeries_GetsTheNextLetter()
    {
        SeriesIdFactory.Member("2026-10-06-03c7", 0).Should().Be("2026-10-06-03c7a");
        SeriesIdFactory.Member("2026-10-06-03c7", 1).Should().Be("2026-10-06-03c7b");
        SeriesIdFactory.Member("2026-10-06-03c7", 2).Should().Be("2026-10-06-03c7c");
    }
}
