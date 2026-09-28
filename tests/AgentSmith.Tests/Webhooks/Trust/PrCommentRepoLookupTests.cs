using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Webhooks;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Webhooks.Trust;

public sealed class PrCommentRepoLookupTests
{
    [Fact]
    public void Find_ReadsTheServersConfigurationAndMatchesAgainstIt()
    {
        var config = new AgentSmithConfig();
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig("server.yml")).Returns(config);
        var expected = new ConfiguredRepo("p", new ResolvedProject(), new RepoConnection { Name = "r" });
        var finder = new Mock<IConfiguredRepoFinder>();
        finder.Setup(f => f.Find(config, "https://host/org/r")).Returns(expected);

        var sut = new PrCommentRepoLookup(loader.Object, new ServerContext("server.yml"), finder.Object);

        sut.Find("https://host/org/r").Should().Be(expected);
    }
}
