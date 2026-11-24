using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Server.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>2026-11-24-35ff: every preset says whether the server starts it itself, and every run payload carries it as
/// <c>system</c>, so the runs list can hide the server's own runs without knowing pipeline names.</summary>
public sealed class PipelinePresetsSystemTests
{
    [Fact]
    public void EveryPreset_DeclaresItsOrigin()
    {
        PipelinePresets.Names.Where(name => PipelinePresets.OriginOf(name) is null)
            .Should().BeEmpty("a new pipeline has to decide in PipelinePresets.System.cs whether it is work or system");
    }

    [Fact]
    public void EveryOrigin_NamesAPreset()
    {
        PipelinePresets.SystemNames.Should().OnlyContain(name => PipelinePresets.IsAcceptedName(name));
    }

    [Theory]
    [InlineData(PipelinePresets.SeriesRunName)]
    [InlineData(PipelinePresets.SeriesVerifyName)]
    [InlineData(PipelinePresets.RefreshBaseName)]
    [InlineData(PipelinePresets.BranchCleanupName)]
    [InlineData(PipelinePresets.StartName)]
    [InlineData(PipelinePresets.RetargetName)]
    [InlineData(PipelinePresets.MergeGreenName)]
    [InlineData(PipelinePresets.PocUploadPurgeName)]
    public void ServerStartedPreset_IsSystem(string preset)
    {
        PipelinePresets.IsSystem(preset).Should().BeTrue();
        PipelinePresets.SystemNames.Should().Contain(preset);
    }

    [Theory]
    [InlineData(PipelinePresets.CodeName)]
    [InlineData(PipelinePresets.DeployName)]
    [InlineData(PipelinePresets.SpecDialogName)]
    [InlineData(PipelinePresets.CloseName)]
    [InlineData("security-scan")]
    public void PersonChosenPreset_IsWork(string preset)
    {
        PipelinePresets.IsSystem(preset).Should().BeFalse();
    }

    [Fact]
    public void UnknownName_IsWork()
    {
        PipelinePresets.OriginOf("no-such-pipeline").Should().BeNull();
        PipelinePresets.IsSystem("no-such-pipeline").Should().BeFalse();
    }

    [Theory]
    [InlineData(PipelinePresets.SeriesRunName, true)]
    [InlineData(PipelinePresets.CodeName, false)]
    public void RunSnapshot_SerializesSystem(string pipeline, bool system)
    {
        var snapshot = RunSnapshot.Empty("run-1") with { Pipeline = pipeline };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        json.RootElement.GetProperty("system").GetBoolean().Should().Be(system);
    }
}
