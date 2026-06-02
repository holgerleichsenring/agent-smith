using AgentSmith.Contracts.Sandbox;
using Moq;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0202d: a reader factory whose reads return null, i.e. the cold-init case
/// (no existing context.yaml / coding-principles.md). Lets BootstrapRound tests
/// that don't exercise the re-init merge keep the generate-from-scratch path.
/// </summary>
internal static class BootstrapReaderStubs
{
    public static ISandboxFileReaderFactory NullReaderFactory() =>
        ReaderFactoryReturning(contextYaml: null);

    /// <summary>Re-init case: the reader serves an existing context.yaml (and
    /// optionally coding-principles.md), so the producer prompt switches to
    /// preserve-and-merge.</summary>
    public static ISandboxFileReaderFactory ReaderFactoryReturning(
        string? contextYaml, string? codingPrinciples = null)
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.TryReadAsync(
                It.Is<string>(p => p.EndsWith("context.yaml")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(contextYaml);
        reader.Setup(r => r.TryReadAsync(
                It.Is<string>(p => p.EndsWith("coding-principles.md")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(codingPrinciples);
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader.Object);
        return factory.Object;
    }
}
