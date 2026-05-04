using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class ProviderOverrideResolverTests : IDisposable
{
    private readonly string _skillDir;

    public ProviderOverrideResolverTests()
    {
        _skillDir = Path.Combine(Path.GetTempPath(), "agentsmith-provider-" + Guid.NewGuid());
        Directory.CreateDirectory(_skillDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_skillDir)) Directory.Delete(_skillDir, recursive: true);
    }

    [Fact]
    public void Resolve_OverrideExists_ReturnsOverridePath()
    {
        File.WriteAllText(Path.Combine(_skillDir, "SKILL.md"), "---\nname: x\n---\n");
        File.WriteAllText(Path.Combine(_skillDir, "SKILL.openai.md"), "---\nname: x\n---\n");
        var sut = new ProviderOverrideResolver(StubProvider("openai"));

        var paths = sut.Resolve(_skillDir);

        paths.EffectivePath.Should().EndWith("SKILL.openai.md");
        paths.BasePath.Should().EndWith("SKILL.md");
    }

    [Fact]
    public void Resolve_OverrideMissing_FallsBackToBase()
    {
        File.WriteAllText(Path.Combine(_skillDir, "SKILL.md"), "---\nname: x\n---\n");
        var sut = new ProviderOverrideResolver(StubProvider("openai"));

        var paths = sut.Resolve(_skillDir);

        paths.EffectivePath.Should().EndWith("SKILL.md").And.NotContain("openai");
        paths.BasePath.Should().BeNull();
    }

    [Fact]
    public void Resolve_DifferentProvider_IgnoresUnrelatedOverrides()
    {
        File.WriteAllText(Path.Combine(_skillDir, "SKILL.md"), "---\nname: x\n---\n");
        File.WriteAllText(Path.Combine(_skillDir, "SKILL.gemini.md"), "---\nname: x\n---\n");
        var sut = new ProviderOverrideResolver(StubProvider("openai"));

        var paths = sut.Resolve(_skillDir);

        paths.EffectivePath.Should().EndWith("SKILL.md").And.NotContain("gemini");
        paths.BasePath.Should().BeNull();
    }

    [Fact]
    public void Resolve_NoActiveProvider_ReturnsBaseRegardlessOfOverrides()
    {
        File.WriteAllText(Path.Combine(_skillDir, "SKILL.md"), "---\nname: x\n---\n");
        File.WriteAllText(Path.Combine(_skillDir, "SKILL.openai.md"), "---\nname: x\n---\n");
        var sut = new ProviderOverrideResolver(StubProvider(string.Empty));

        var paths = sut.Resolve(_skillDir);

        paths.EffectivePath.Should().EndWith("SKILL.md").And.NotContain("openai");
        paths.BasePath.Should().BeNull();
    }

    private static IActiveProviderResolver StubProvider(string name)
    {
        var mock = new Mock<IActiveProviderResolver>();
        mock.Setup(x => x.GetActiveProvider()).Returns(name);
        return mock.Object;
    }
}
