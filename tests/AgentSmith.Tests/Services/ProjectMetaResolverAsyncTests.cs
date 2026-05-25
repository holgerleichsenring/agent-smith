using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class ProjectMetaResolverAsyncTests
{
    [Fact]
    public async Task ResolveAllAsync_MultipleContexts_ReturnsAll()
    {
        var reader = ReaderWithSubDirs(
            "/work/.agentsmith/contexts/server",
            "/work/.agentsmith/contexts/client",
            "/work/.agentsmith/contexts/docs");
        SetupYaml(reader, "/work/.agentsmith/contexts/server/context.yaml", "server-yaml");
        SetupYaml(reader, "/work/.agentsmith/contexts/client/context.yaml", "client-yaml");
        SetupYaml(reader, "/work/.agentsmith/contexts/docs/context.yaml", "docs-yaml");

        var parser = new Mock<IContextYamlParser>();
        parser.Setup(p => p.TryParse("server-yaml")).Returns(new ContextYamlSummary("src/Server", "csharp"));
        parser.Setup(p => p.TryParse("client-yaml")).Returns(new ContextYamlSummary("src/Client", "typescript"));
        parser.Setup(p => p.TryParse("docs-yaml")).Returns(new ContextYamlSummary("docs", "markdown"));

        var result = await new ProjectMetaResolver(parser.Object).ResolveAllAsync(
            reader.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(new[]
        {
            new MetaDiscovery("/work/.agentsmith/contexts/server", "server", "src/Server"),
            new MetaDiscovery("/work/.agentsmith/contexts/client", "client", "src/Client"),
            new MetaDiscovery("/work/.agentsmith/contexts/docs", "docs", "docs"),
        });
    }

    [Fact]
    public async Task ResolveAllAsync_SingleDefaultContext_OneEntry()
    {
        var reader = ReaderWithSubDirs("/work/.agentsmith/contexts/default");
        SetupYaml(reader, "/work/.agentsmith/contexts/default/context.yaml", "default-yaml");

        var parser = new Mock<IContextYamlParser>();
        parser.Setup(p => p.TryParse("default-yaml")).Returns(new ContextYamlSummary(".", "csharp"));

        var result = await new ProjectMetaResolver(parser.Object).ResolveAllAsync(
            reader.Object, CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Should().Be(new MetaDiscovery("/work/.agentsmith/contexts/default", "default", "."));
    }

    [Fact]
    public async Task ResolveAllAsync_NoContextsDir_ReturnsEmpty()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ListAsync("/work/.agentsmith/contexts", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        var result = await new ProjectMetaResolver(Mock.Of<IContextYamlParser>()).ResolveAllAsync(
            reader.Object, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAllAsync_SubDirMissingContextYaml_Skips()
    {
        var reader = ReaderWithSubDirs(
            "/work/.agentsmith/contexts/server",
            "/work/.agentsmith/contexts/broken");
        SetupYaml(reader, "/work/.agentsmith/contexts/server/context.yaml", "server-yaml");
        SetupYaml(reader, "/work/.agentsmith/contexts/broken/context.yaml", null);

        var parser = new Mock<IContextYamlParser>();
        parser.Setup(p => p.TryParse("server-yaml")).Returns(new ContextYamlSummary("src/Server", "csharp"));

        var result = await new ProjectMetaResolver(parser.Object).ResolveAllAsync(
            reader.Object, CancellationToken.None);

        result.Should().ContainSingle().Which.ContextName.Should().Be("server");
    }

    [Fact]
    public async Task ResolveAllAsync_ParserReturnsNull_Skips()
    {
        var reader = ReaderWithSubDirs("/work/.agentsmith/contexts/garbage");
        SetupYaml(reader, "/work/.agentsmith/contexts/garbage/context.yaml", "not-yaml");

        var parser = new Mock<IContextYamlParser>();
        parser.Setup(p => p.TryParse("not-yaml")).Returns((ContextYamlSummary?)null);

        var result = await new ProjectMetaResolver(parser.Object).ResolveAllAsync(
            reader.Object, CancellationToken.None);

        result.Should().BeEmpty();
    }

    private static Mock<ISandboxFileReader> ReaderWithSubDirs(params string[] subDirs)
    {
        var mock = new Mock<ISandboxFileReader>();
        mock.Setup(r => r.ListAsync("/work/.agentsmith/contexts", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subDirs);
        return mock;
    }

    private static void SetupYaml(Mock<ISandboxFileReader> reader, string path, string? content)
    {
        reader.Setup(r => r.TryReadAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
    }
}
