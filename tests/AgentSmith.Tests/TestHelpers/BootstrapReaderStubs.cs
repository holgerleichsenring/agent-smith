using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0202d: a reader factory whose reads return null, i.e. the cold-init case
/// (no existing context.yaml / principles.md). Lets BootstrapRound tests
/// that don't exercise the re-init merge keep the generate-from-scratch path.
/// </summary>
internal static class BootstrapReaderStubs
{
    public static ISandboxFileReaderFactory NullReaderFactory() =>
        ReaderFactoryReturning(contextYaml: null);

    /// <summary>2026-09-01-72c5: the round reads its meta files through
    /// <see cref="BootstrapMetaFiles"/>. Cold-init case.</summary>
    public static BootstrapMetaFiles NullMetaFiles() => MetaFilesReturning(contextYaml: null);

    /// <summary>Re-init case, seen through the round's meta-file reader.</summary>
    public static BootstrapMetaFiles MetaFilesReturning(
        string? contextYaml, string? principles = null) =>
        MetaFilesOver(ReaderFactoryReturning(contextYaml, principles));

    public static BootstrapMetaFiles MetaFilesOver(ISandboxFileReaderFactory readers) =>
        new(readers, NullLogger<BootstrapMetaFiles>.Instance);

    /// <summary>Re-init case: the reader serves an existing context.yaml (and
    /// optionally principles.md), so the producer prompt switches to
    /// preserve-and-merge.</summary>
    public static ISandboxFileReaderFactory ReaderFactoryReturning(
        string? contextYaml, string? codingPrinciples = null)
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.TryReadAsync(
                It.Is<string>(p => p.EndsWith("context.yaml")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(contextYaml);
        reader.Setup(r => r.TryReadAsync(
                It.Is<string>(p => p.EndsWith("principles.md")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(codingPrinciples);
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader.Object);
        return factory.Object;
    }
}
