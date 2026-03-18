using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class StorageReaderRegistryTests
{
    private static AttachmentRef CreateRef(string uri = "file:///tmp/doc.pdf") =>
        new(uri, "doc.pdf", "application/pdf");

    [Fact]
    public void Resolve_returns_first_reader_that_can_handle()
    {
        var ref1 = CreateRef();
        var reader1 = new Mock<IStorageReader>();
        reader1.Setup(r => r.CanHandle(ref1)).Returns(false);
        var reader2 = new Mock<IStorageReader>();
        reader2.Setup(r => r.CanHandle(ref1)).Returns(true);

        var registry = new StorageReaderRegistry([reader1.Object, reader2.Object]);

        registry.Resolve(ref1).Should().BeSameAs(reader2.Object);
    }

    [Fact]
    public void Resolve_throws_when_no_reader_can_handle()
    {
        var ref1 = CreateRef("s3://bucket/key");
        var reader = new Mock<IStorageReader>();
        reader.Setup(r => r.CanHandle(ref1)).Returns(false);

        var registry = new StorageReaderRegistry([reader.Object]);

        var act = () => registry.Resolve(ref1);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*doc.pdf*");
    }

    [Fact]
    public void TryResolve_returns_true_when_reader_found()
    {
        var ref1 = CreateRef();
        var reader = new Mock<IStorageReader>();
        reader.Setup(r => r.CanHandle(ref1)).Returns(true);

        var registry = new StorageReaderRegistry([reader.Object]);

        registry.TryResolve(ref1, out var result).Should().BeTrue();
        result.Should().BeSameAs(reader.Object);
    }

    [Fact]
    public void TryResolve_returns_false_when_no_reader_found()
    {
        var ref1 = CreateRef();
        var reader = new Mock<IStorageReader>();
        reader.Setup(r => r.CanHandle(ref1)).Returns(false);

        var registry = new StorageReaderRegistry([reader.Object]);

        registry.TryResolve(ref1, out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void Empty_registry_throws_on_resolve()
    {
        var registry = new StorageReaderRegistry([]);

        var act = () => registry.Resolve(CreateRef());
        act.Should().Throw<InvalidOperationException>();
    }
}
