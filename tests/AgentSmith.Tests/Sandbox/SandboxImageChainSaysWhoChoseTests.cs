using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-24-7e4b: four links of the chain decided in silence, so an operator looking at a
/// wrong image could not tell an override from a catalog hit from a context image the registry
/// policy had REFUSED. The last link is worse than silent: it hands back a Debian image with
/// git and no language toolchain, which fails later as a command-not-found inside a sandbox,
/// where it reads as the run's problem rather than the image's.
/// </summary>
public sealed class SandboxImageChainSaysWhoChoseTests
{
    private readonly RecordingLogger _log = new();

    private SandboxImageChain Chain() => new(new ImageRegistryTrust(), _log);

    [Fact]
    public void Resolve_ALanguageTheCatalogDoesNotKnow_SaysItFellBackToAnImageWithNoToolchain()
    {
        var image = Chain().Resolve(new ResolvedProject(), "cobol", contextImage: null);

        image.Should().Be(SandboxImageChain.GenericFallbackImage);
        var warning = _log.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning).Subject;
        warning.Message.Should().Contain("cobol")
            .And.Contain("no language toolchain")
            .And.Contain("stack.image", "the operator is told where to say it")
            .And.Contain("csharp", "and which languages are already known");
    }

    [Fact]
    public void Resolve_ALanguageTheCatalogKnows_DoesNotAnnounceAFallback()
    {
        Chain().Resolve(new ResolvedProject(), "python", contextImage: null)
            .Should().Be("python:3.12-bookworm");

        _log.Entries.Should().NotContain(e => e.Level == LogLevel.Warning);
        _log.Entries.Should().ContainSingle().Which.Message.Should().Contain("the language catalog");
    }

    [Fact]
    public void Resolve_AProjectOverride_NamesTheOverrideAsTheDecidingLink()
    {
        var project = new ResolvedProject
        {
            Sandbox = new SandboxConfig { ToolchainImage = "ghcr.io/acme/toolbox:1" },
        };

        Chain().Resolve(project, "csharp", contextImage: null).Should().Be("ghcr.io/acme/toolbox:1");

        _log.Entries.Should().ContainSingle().Which.Message
            .Should().Contain("sandbox.toolchain_image").And.Contain("ghcr.io/acme/toolbox:1");
    }

    [Fact]
    public void Resolve_AContextImageOutsideTrust_SaysTrustRefusedItAndWhereTheBoundaryRuns()
    {
        // The refusal is the operator's own policy acting. Reported as "no image was declared"
        // it looks like a gap in the context; reported as a refusal it looks like what it is.
        var image = Chain().Resolve(new ResolvedProject(), "csharp", "quay.io/someone/sdk:10.0");

        image.Should().Be("mcr.microsoft.com/dotnet/sdk:10.0", "the chain continues past a refusal");
        _log.Entries.Should().Contain(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("outside the trusted registries"));
    }

    private sealed record Entry(LogLevel Level, string Message);

    private sealed class RecordingLogger : ILogger<SandboxImageChain>
    {
        public List<Entry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new Entry(logLevel, formatter(state, exception)));
    }
}
