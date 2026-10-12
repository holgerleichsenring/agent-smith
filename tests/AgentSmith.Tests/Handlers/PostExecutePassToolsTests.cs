using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-10-12-f5f7c: a post-execute pass's decisions go to the changed repository they name —
/// the pass no longer hands the log the first changed sandbox.
/// </summary>
public sealed class PostExecutePassToolsTests
{
    [Fact]
    public async Task For_TwoChangedRepos_DecisionFileGoesToTheNamedOne()
    {
        var (server, client) = (Mock.Of<ISandbox>(), Mock.Of<ISandbox>());
        var (serverFiles, clientFiles) = (Mock.Of<ISandboxFileReader>(), Mock.Of<ISandboxFileReader>());
        var readers = new Mock<ISandboxFileReaderFactory>();
        readers.Setup(f => f.Create(server)).Returns(serverFiles);
        readers.Setup(f => f.Create(client)).Returns(clientFiles);
        var logger = new Mock<IDecisionLogger>();
        logger.Setup(l => l.LogAsync(It.IsAny<ISandboxFileReader?>(), It.IsAny<DecisionCategory>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(DecisionLogOutcome.Recorded);
        var partition = new RepoDiffPartition(
            new Dictionary<string, ISandbox> { ["server-csharp"] = server, ["client"] = client },
            ["server", "client"], [],
            new Dictionary<string, ISandbox> { ["server"] = server, ["client"] = client });

        var (_, log, _) = new PostExecutePassTools(logger.Object, readers.Object, null).For(partition, "/work");
        await log.LogDecision("Implementation", "client tests use the shared fixture", "client");

        logger.Verify(l => l.LogAsync(clientFiles, DecisionCategory.Implementation,
            "client tests use the shared fixture", It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public void For_NothingChanged_Throws()
    {
        var partition = new RepoDiffPartition(
            new Dictionary<string, ISandbox>(), [], ["server"], new Dictionary<string, ISandbox>());

        var act = () => new PostExecutePassTools(Mock.Of<IDecisionLogger>(),
            Mock.Of<ISandboxFileReaderFactory>(), null).For(partition, "/work");

        act.Should().Throw<ArgumentException>();
    }
}
