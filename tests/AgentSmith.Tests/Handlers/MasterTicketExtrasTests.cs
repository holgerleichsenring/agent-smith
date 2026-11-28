using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-11-28-db88b: a ticket's documents are written beside the uploaded images, in the directory the clone's own
/// exclude keeps out of every commit — no run record under .agentsmith/runs/ carries them.
/// </summary>
public sealed class MasterTicketExtrasTests
{
    private readonly InMemorySandboxFileReader _files = new();
    private readonly RecordingMaterializer _materializer = new();

    [Fact]
    public async Task Compose_TicketDocuments_LandInTheExcludedReferenceDirectory()
    {
        var context = MasterHandlerFixture.BuildContext("coding-agent-master");
        context.Pipeline.Set<IReadOnlyList<TicketDocumentAttachment>>(ContextKeys.TicketDocuments,
            [new TicketDocumentAttachment(new AttachmentRef("https://t/1", "notes.txt", "text/plain"), "the requirement"u8.ToArray())]);

        await Extras().ComposeAsync(context, Mock.Of<ISandbox>(), string.Empty, isScanMaster: false, CancellationToken.None);

        _materializer.Directory.Should().Be(".agentsmith/reference/documents");
        _files.Files[".git/info/exclude"].Should().Contain("/.agentsmith/reference/");
    }

    [Fact]
    public async Task Compose_NoDocuments_WritesNothing()
    {
        var context = MasterHandlerFixture.BuildContext("coding-agent-master");

        await Extras().ComposeAsync(context, Mock.Of<ISandbox>(), string.Empty, isScanMaster: false, CancellationToken.None);

        _materializer.Directory.Should().BeNull();
        _files.Files.Should().BeEmpty();
    }

    private MasterTicketExtras Extras()
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(_files);
        return new MasterTicketExtras(_materializer, new ReferenceGitExclusion(factory.Object));
    }

    private sealed class RecordingMaterializer : ITicketDocumentMaterializer
    {
        public string? Directory { get; private set; }

        public Task<IReadOnlyList<MaterializedTicketDocument>> MaterializeAsync(
            ISandbox sandbox, string directory, IReadOnlyList<TicketDocumentAttachment> documents, CancellationToken cancellationToken)
        {
            Directory = directory;
            return Task.FromResult<IReadOnlyList<MaterializedTicketDocument>>([]);
        }
    }
}
