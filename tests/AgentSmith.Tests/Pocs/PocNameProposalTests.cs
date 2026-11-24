using AgentSmith.Application.Services.Pocs;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs;

/// <summary>2026-11-23-933db: a new PoC's name is proposed from the ZIP's file name as a DNS label of at most 40.</summary>
public sealed class PocNameProposalTests
{
    [Theory]
    [InlineData("My Cool App (2).zip", "my-cool-app-2")]
    [InlineData("C:\\Users\\ann\\Downloads\\Shop--Front_v1.ZIP", "shop-front-v1")]
    [InlineData("((( ))).zip", "app")]
    public void From_AFileName_ProposesADnsLabel(string fileName, string name)
    {
        PocNameProposal.From(fileName).Should().Be(name);
    }

    [Fact]
    public void From_ASeventyCharacterName_IsTrimmedTo40AtALabelBoundary()
    {
        var fileName = "customer-onboarding-dashboard-with-reporting-and-export-feature-v2.zip";
        fileName.Length.Should().Be(70);

        var name = PocNameProposal.From(fileName);

        name.Should().Be("customer-onboarding-dashboard-with");
        PocNameProposal.IsName(name).Should().BeTrue();
    }

    [Fact]
    public void From_ALongNameWithoutABoundary_IsCutAt40()
    {
        PocNameProposal.From(new string('a', 70) + ".zip").Should().Be(new string('a', 40));
    }

    [Theory]
    [InlineData("my-app", true)]
    [InlineData("9lives", true)]
    [InlineData("My-App", false)]
    [InlineData("-app", false)]
    [InlineData("app-", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsName_IsADnsLabelOfAtMost40(string? name, bool expected)
    {
        PocNameProposal.IsName(name).Should().Be(expected);
        PocNameProposal.IsName(new string('a', 41)).Should().BeFalse();
    }
}
