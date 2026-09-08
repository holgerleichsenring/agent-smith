using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-09-04-cf3d: one named meta file from every context of a sandbox, in context order;
/// a context without the file contributes nothing and does not stop the others.
/// </summary>
public sealed class ContextDocumentReaderTests
{
    [Fact]
    public async Task ReadAsync_TwoContexts_ReadsBothInOrderAttributed()
    {
        var files = new InMemorySandboxFileReader();
        files.Files["/work/.agentsmith/contexts/backend/principles.md"] = "# Backend";
        files.Files["/work/.agentsmith/contexts/frontend/principles.md"] = "# Frontend";
        var contexts = new[]
        {
            new RemoteContextDiscovery("backend", "backend", "typescript"),
            new RemoteContextDiscovery("frontend", "frontend", "typescript"),
        };

        var documents = await Reader(files).ReadAsync(
            Mock.Of<ISandbox>(), "primary", contexts, "principles.md", CancellationToken.None);

        documents.Select(d => (d.SandboxKey, d.ContextName, d.Workdir, d.Path, d.Content)).Should().Equal(
            ("primary", "backend", "backend", "/work/.agentsmith/contexts/backend/principles.md", "# Backend"),
            ("primary", "frontend", "frontend", "/work/.agentsmith/contexts/frontend/principles.md", "# Frontend"));
    }

    [Fact]
    public async Task ReadAsync_AContextWithoutTheFile_IsSkipped()
    {
        var files = new InMemorySandboxFileReader();
        files.Files["/work/.agentsmith/contexts/frontend/context.yaml"] = "meta: {}";
        var contexts = new[]
        {
            new RemoteContextDiscovery("backend", "backend", "typescript"),
            new RemoteContextDiscovery("frontend", "frontend", "typescript"),
        };

        var documents = await Reader(files).ReadAsync(
            Mock.Of<ISandbox>(), "primary", contexts, "context.yaml", CancellationToken.None);

        documents.Should().ContainSingle().Which.ContextName.Should().Be("frontend");
    }

    private static ContextDocumentReader Reader(ISandboxFileReader files)
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(files);
        return new ContextDocumentReader(factory.Object);
    }
}
