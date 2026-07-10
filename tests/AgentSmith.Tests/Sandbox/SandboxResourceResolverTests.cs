using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Sandbox;

public sealed class SandboxResourceResolverTests
{
    private static readonly ContextYamlStackResources ValidContext =
        new("500m", "2", "1Gi", "4Gi");

    private static SandboxResourceResolver NewSut(SandboxOptions? options = null, ILogger<SandboxResourceResolver>? logger = null) =>
        new(Options.Create(options ?? new SandboxOptions()), logger ?? NullLogger<SandboxResourceResolver>.Instance);

    [Fact]
    public void Resolve_FixBug_UsesContextResources()
    {
        var project = new ResolvedProject { Sandbox = null };
        var sut = NewSut(new SandboxOptions
        {
            CpuRequest = "100m", CpuLimit = "500m", MemoryRequest = "256Mi", MemoryLimit = "512Mi"
        });

        var resolved = sut.Resolve(project, "fix-bug", ValidContext);

        // p0268: a valid context block sizes the sandbox over the global default.
        resolved.Should().Be(new ResourceLimits("500m", "2", "1Gi", "4Gi"));
    }

    [Fact]
    public void Resolve_ProjectOverride_BeatsContextResources()
    {
        var projectResources = new ResourceLimits("250m", "1000m", "512Mi", "2Gi");
        var project = new ResolvedProject { Sandbox = new SandboxConfig { Resources = projectResources } };
        var sut = NewSut();

        var resolved = sut.Resolve(project, "fix-bug", ValidContext);

        // Operator authority beats the LLM guess — project wins even with a valid context.
        resolved.Should().BeSameAs(projectResources);
    }

    [Fact]
    public void Resolve_InvalidContextQuantity_FallsBackToGlobalWithWarning()
    {
        var project = new ResolvedProject { Sandbox = null };
        var logger = new CapturingLogger();
        var sut = NewSut(new SandboxOptions
        {
            CpuRequest = "100m", CpuLimit = "500m", MemoryRequest = "256Mi", MemoryLimit = "512Mi"
        }, logger);
        var bad = new ContextYamlStackResources("500m", "lots", "1Gi", "4Gi");

        var resolved = sut.Resolve(project, "fix-bug", bad);

        resolved.Should().Be(new ResourceLimits("100m", "500m", "256Mi", "512Mi"));
        logger.Warnings.Should().ContainSingle().Which.Should().Contain("unparseable");
    }

    [Fact]
    public void Resolve_PartialContextResources_RejectsWholeBlockWithWarning()
    {
        var project = new ResolvedProject { Sandbox = null };
        var logger = new CapturingLogger();
        var sut = NewSut(logger: logger);
        // memory_limit missing → the whole block is rejected, not silently completed.
        var partial = new ContextYamlStackResources("500m", "2", "1Gi", null);

        var resolved = sut.Resolve(project, "fix-bug", partial);

        resolved.Should().Be(ResourceLimits.Default);
        logger.Warnings.Should().ContainSingle().Which.Should().Contain("partial");
    }

    [Fact]
    public void Resolve_NoContextResources_ReturnsGlobalDefaultWithoutWarning()
    {
        var project = new ResolvedProject { Sandbox = null };
        var logger = new CapturingLogger();
        var sut = NewSut(logger: logger);

        var resolved = sut.Resolve(project, "fix-bug", contextResources: null);

        resolved.Should().Be(ResourceLimits.Default);
        logger.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_InitProject_IgnoresContextResources_UsesLightProfile()
    {
        // p0320a: init-project clones and writes files but never compiles — the
        // LLM-authored build sizing must not apply, no matter what it says.
        var project = new ResolvedProject { Sandbox = null };
        var sut = NewSut();

        var resolved = sut.Resolve(project, "init-project", ValidContext);

        resolved.Should().BeSameAs(ResourceLimits.LightProfile);
    }

    [Theory]
    [InlineData("init-project")]
    [InlineData("security-scan")]
    [InlineData("legal-analysis")]
    [InlineData("fix-bug")]
    public void Resolve_ProjectOverride_WinsForAllPipelines(string pipelineName)
    {
        // p0320a: operator authority beats both the light profile and the LLM guess.
        var projectResources = new ResourceLimits("250m", "1000m", "512Mi", "2Gi");
        var project = new ResolvedProject { Sandbox = new SandboxConfig { Resources = projectResources } };
        var sut = NewSut();

        var resolved = sut.Resolve(project, pipelineName, ValidContext);

        resolved.Should().BeSameAs(projectResources);
    }

    [Fact]
    public void Resolve_ContextResourcesAboveCeiling_Clamped_WithWarn()
    {
        // p0320a: over-sized LLM values are clamped to the SandboxOptions ceiling
        // (default 2 cpu / 6Gi), requests AND limits, with a single WARN.
        var project = new ResolvedProject { Sandbox = null };
        var logger = new CapturingLogger();
        var sut = NewSut(logger: logger);
        var oversized = new ContextYamlStackResources("3", "4", "8Gi", "12Gi");

        var resolved = sut.Resolve(project, "fix-bug", oversized);

        resolved.Should().Be(new ResourceLimits("2", "2", "6Gi", "6Gi"));
        logger.Warnings.Should().ContainSingle().Which.Should().Contain("ceiling")
            .And.Contain("4").And.Contain("12Gi");
    }

    private sealed class CapturingLogger : ILogger<SandboxResourceResolver>
    {
        public List<string> Warnings { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Warnings.Add(formatter(state, exception));
        }
    }

    [Fact]
    public void Resolve_ProjectHasResourceBlock_ReturnsProjectResources()
    {
        var projectResources = new ResourceLimits("500m", "2000m", "1Gi", "4Gi");
        var project = new ResolvedProject { Sandbox = new SandboxConfig { Resources = projectResources } };
        var sut = new SandboxResourceResolver(Options.Create(new SandboxOptions()));

        var resolved = sut.Resolve(project, "fix-bug");

        resolved.Should().BeSameAs(projectResources);
    }

    [Fact]
    public void Resolve_ProjectSandboxResourcesNull_ReturnsGlobalDefaults()
    {
        var project = new ResolvedProject { Sandbox = new SandboxConfig { Resources = null } };
        var sut = new SandboxResourceResolver(Options.Create(new SandboxOptions
        {
            CpuRequest = "300m", CpuLimit = "1500m", MemoryRequest = "768Mi", MemoryLimit = "3Gi"
        }));

        var resolved = sut.Resolve(project, "fix-bug");

        resolved.Should().Be(new ResourceLimits("300m", "1500m", "768Mi", "3Gi"));
    }

    [Fact]
    public void Resolve_ProjectSandboxNull_ReturnsGlobalDefaults()
    {
        var project = new ResolvedProject { Sandbox = null };
        var sut = new SandboxResourceResolver(Options.Create(new SandboxOptions()));

        var resolved = sut.Resolve(project, "fix-bug");

        resolved.Should().Be(ResourceLimits.Default);
    }
}
