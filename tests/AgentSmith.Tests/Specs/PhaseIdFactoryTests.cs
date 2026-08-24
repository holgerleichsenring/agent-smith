using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0509: the factory that mints every phase id a run writes into a target repository had
/// no tests at all. An id is an identity a later run must be able to RECOMPUTE, so the
/// shape it produces is a contract — and it is the shape the phase-record rules have to
/// be able to read back.
/// </summary>
public sealed class PhaseIdFactoryTests
{
    /// <summary>
    /// A real ticket number is longer than the four digits the record used to read. Six
    /// digits is the deliberate ceiling — beyond it the last six are kept, so an id stays
    /// short enough to be a filename.
    /// </summary>
    [Fact]
    public void PhaseIdFactory_LongTicketNumber_KeepsEveryDigit()
    {
        PhaseIdFactory.For("19106", 0).Should().Be("p19106a");
        PhaseIdFactory.For("482913", 0).Should().Be("p482913a");
    }

    [Fact]
    public void PhaseIdFactory_SecondPhaseOfATicket_GetsTheNextLetter()
    {
        PhaseIdFactory.For("19106", 0).Should().Be("p19106a");
        PhaseIdFactory.For("19106", 1).Should().Be("p19106b");
        PhaseIdFactory.For("19106", 2).Should().Be("p19106c");
    }

    /// <summary>
    /// Trackers spell a ticket id every way there is. Digits survive; a ticket carrying no
    /// digit at all falls back to a stable four-digit hash, because an id that changed
    /// between runs would break the re-cut's identity rule.
    /// </summary>
    [Fact]
    public void PhaseIdFactory_TicketIdWithNonDigits_KeepsOnlyTheDigits()
    {
        PhaseIdFactory.For("PROJ-19106", 0).Should().Be("p19106a");
        PhaseIdFactory.For("#57", 0).Should().Be("p0057a");
        PhaseIdFactory.For("release", 0).Should().Be(PhaseIdFactory.For("release", 0));
        PhaseIdFactory.For("release", 0).Should().MatchRegex("^p[0-9]{4}a$");
    }
}
