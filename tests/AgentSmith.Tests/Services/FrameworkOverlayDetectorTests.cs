using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-10-03-cf20c: whether a component's manifest declares a framework is READ against the
/// catalog's signals — and any read that fails applies nothing.
/// </summary>
public sealed class FrameworkOverlayDetectorTests
{
    private static readonly FrameworkOverlay Spark = new("spark",
    [
        new FrameworkOverlaySignal("build.sbt", "org.apache.spark"),
        new FrameworkOverlaySignal("requirements*.txt", "pyspark"),
    ]);

    private readonly CapturingLogger<FrameworkOverlayDetector> _logger = new();

    [Fact]
    public async Task FrameworkOverlayDetector_DependencyInManifest_AppliesTheOverlay()
    {
        var reader = new FakeReader { Files = { ["jobs/build.sbt"] = "libraryDependencies += \"org.apache.spark\" %% \"spark-sql\"" } };

        (await DetectAsync(reader, "jobs")).Should().Equal("spark");
    }

    [Fact]
    public async Task FrameworkOverlayDetector_ManifestWithoutTheDependency_AppliesNothing()
    {
        var reader = new FakeReader { Files = { ["jobs/build.sbt"] = "libraryDependencies += \"org.typelevel\" %% \"cats-core\"" } };

        (await DetectAsync(reader, "jobs")).Should().BeEmpty();
    }

    [Fact]
    public async Task FrameworkOverlayDetector_GlobSignal_MatchesRequirementsFiles()
    {
        var reader = new FakeReader
        {
            Files = { ["/work/etl/requirements-dev.txt"] = "pytest\npyspark==3.5.1\n" },
            Listings = { ["etl"] = ["/work/etl/requirements-dev.txt", "/work/etl/requirements.d", "/work/etl/setup.py"] },
        };

        (await DetectAsync(reader, "etl/")).Should().Equal("spark");
        reader.Listed.Should().Equal("etl");
    }

    [Fact]
    public async Task FrameworkOverlayDetector_ReadThrows_AppliesNothingAndLogs()
    {
        var reader = new FakeReader { Throws = true };

        (await DetectAsync(reader, ".")).Should().BeEmpty();
        _logger.Warnings.Should().ContainSingle(w => w.Contains("spark", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/etc")]
    [InlineData("../sibling")]
    [InlineData("jobs/../..")]
    public async Task FrameworkOverlayDetector_EscapingWorkdir_AppliesNothing(string workdir)
    {
        var reader = new FakeReader { Files = { [$"{workdir}/build.sbt"] = "org.apache.spark" } };

        (await DetectAsync(reader, workdir)).Should().BeEmpty();
        reader.Read.Should().BeEmpty("a workdir outside the component root is never read");
    }

    private Task<IReadOnlyList<string>> DetectAsync(FakeReader reader, string workdir)
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader);
        return new FrameworkOverlayDetector(factory.Object, _logger)
            .DetectAsync(Mock.Of<ISandbox>(), workdir, [Spark], CancellationToken.None);
    }

    private sealed class FakeReader : ISandboxFileReader
    {
        public Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, IReadOnlyList<string>> Listings { get; } = new(StringComparer.Ordinal);
        public List<string> Read { get; } = [];
        public List<string> Listed { get; } = [];
        public bool Throws { get; init; }

        public Task<string?> TryReadAsync(string path, CancellationToken ct)
        {
            if (Throws) throw new InvalidOperationException("malformed sandbox reply");
            Read.Add(path);
            return Task.FromResult(Files.GetValueOrDefault(path));
        }

        public Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken ct)
        {
            Listed.Add(path);
            maxDepth.Should().Be(1, "a glob matches direct children only");
            return Task.FromResult(Listings.GetValueOrDefault(path) ?? []);
        }

        public Task<bool> ExistsAsync(string path, CancellationToken ct) => throw new NotSupportedException();
        public Task<string> ReadRequiredAsync(string path, CancellationToken ct) => throw new NotSupportedException();
        public Task WriteAsync(string path, string content, CancellationToken ct) => throw new NotSupportedException();
    }
}
