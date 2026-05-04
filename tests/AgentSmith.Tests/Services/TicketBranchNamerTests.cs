using AgentSmith.Application.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class TicketBranchNamerTests
{
    [Fact]
    public void Compose_SingleSegment_ReturnsAgentSmithSlashTicketId()
    {
        var branch = TicketBranchNamer.Compose(new TicketId("18693"));
        branch.Value.Should().Be("agent-smith/18693");
    }

    [Fact]
    public void Compose_HierarchicalForm_LowercasesAndSlugifiesPlatformAndProject()
    {
        var branch = TicketBranchNamer.Compose("AzureRepos", "Cloud Development", new TicketId("18693"));
        branch.Value.Should().Be("agent-smith/azurerepos/cloud-development/18693");
    }

    [Fact]
    public void Compose_HierarchicalForm_UnicodeProjectNameSlugifiesToAscii()
    {
        var branch = TicketBranchNamer.Compose("github", "Käseträger Über-Service", new TicketId("42"));
        branch.Value.Should().Be("agent-smith/github/k-setr-ger-ber-service/42");
    }

    [Fact]
    public void Compose_HierarchicalForm_EmptyProjectName_Throws()
    {
        var act = () => TicketBranchNamer.Compose("github", "", new TicketId("1"));
        act.Should().Throw<ConfigurationException>().WithMessage("*projectName must not be empty*");
    }

    [Fact]
    public void Compose_HierarchicalForm_AllPunctuationProjectName_ThrowsAfterSlugify()
    {
        var act = () => TicketBranchNamer.Compose("github", "!!!---???", new TicketId("1"));
        act.Should().Throw<ConfigurationException>().WithMessage("*empty slug*");
    }

    [Fact]
    public void Compose_HierarchicalForm_OversizeProjectName_TruncatesWithSha1Suffix()
    {
        var longName = new string('a', 200);
        var branch = TicketBranchNamer.Compose("github", longName, new TicketId("1"));

        branch.Value.Should().StartWith("agent-smith/github/");
        // Slug is capped at 64 chars: 56-char head + dash + 7-char hex hash
        var slug = branch.Value.Split('/')[2];
        slug.Length.Should().Be(64);
        slug.Should().MatchRegex(@"^a{56}-[0-9a-f]{7}$");
    }

    [Fact]
    public void Compose_HierarchicalForm_StableForSameInputs()
    {
        var b1 = TicketBranchNamer.Compose("AzureRepos", "Cloud Development", new TicketId("18693"));
        var b2 = TicketBranchNamer.Compose("AzureRepos", "Cloud Development", new TicketId("18693"));
        b1.Value.Should().Be(b2.Value);
    }
}
