using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

public class PipelinePresetsTests
{
    [Theory]
    [InlineData("fix-bug")]
    [InlineData("fix-no-test")]
    [InlineData("init-project")]
    [InlineData("add-feature")]
    public void TryResolve_KnownPreset_ReturnsCommands(string name)
    {
        var result = PipelinePresets.TryResolve(name);

        result.Should().NotBeNull();
        result!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TryResolve_UnknownPreset_ReturnsNull()
    {
        PipelinePresets.TryResolve("nonexistent").Should().BeNull();
    }

    [Fact]
    public void TryResolve_CaseInsensitive()
    {
        PipelinePresets.TryResolve("Fix-Bug").Should().NotBeNull();
        PipelinePresets.TryResolve("INIT-PROJECT").Should().NotBeNull();
    }

    [Fact]
    public void FixBug_ContainsExpectedCommands()
    {
        PipelinePresets.FixBug.Should().Contain(CommandNames.FetchTicket);
        PipelinePresets.FixBug.Should().Contain(CommandNames.Test);
        PipelinePresets.FixBug.Should().Contain(CommandNames.CommitAndPR);
    }

    [Fact]
    public void FixNoTest_DoesNotContainTestCommand()
    {
        PipelinePresets.FixNoTest.Should().NotContain(CommandNames.Test);
        PipelinePresets.FixNoTest.Should().Contain(CommandNames.CommitAndPR);
    }

    [Fact]
    public void InitProject_HasMinimalCommands()
    {
        PipelinePresets.InitProject.Should().HaveCount(3);
        PipelinePresets.InitProject.Should().Contain(CommandNames.CheckoutSource);
        PipelinePresets.InitProject.Should().Contain(CommandNames.BootstrapProject);
        PipelinePresets.InitProject.Should().Contain(CommandNames.InitCommit);
    }

    [Fact]
    public void AddFeature_ContainsGenerateTestsAndDocs()
    {
        PipelinePresets.AddFeature.Should().Contain(CommandNames.GenerateTests);
        PipelinePresets.AddFeature.Should().Contain(CommandNames.GenerateDocs);
    }
}
