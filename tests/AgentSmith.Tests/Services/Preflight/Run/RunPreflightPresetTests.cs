using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Preflight.Run;

/// <summary>
/// p0428: where the gate sits IS the feature — after checkout so the sandboxes and the
/// branch exist to be read, and before the credential staging an unwritable home kills.
/// </summary>
public sealed class RunPreflightPresetTests
{
    [Fact]
    public void TheGate_RunsBeforeRegistryAuth()
    {
        var code = PipelinePresets.Code;

        code.Should().Contain(CommandNames.RunPreflight);
        code.ToList().IndexOf(CommandNames.RunPreflight)
            .Should().BeGreaterThan(code.ToList().IndexOf(CommandNames.CheckoutSource))
            .And.BeLessThan(code.ToList().IndexOf(CommandNames.SetupRegistryAuth));
    }

    [Fact]
    public void TheGate_RunsBeforeEveryExpensiveModelStep()
    {
        var code = PipelinePresets.Code.ToList();
        var gate = code.IndexOf(CommandNames.RunPreflight);

        code.IndexOf(CommandNames.AnalyzeCode).Should().BeGreaterThan(gate);
        code.IndexOf(CommandNames.DeriveSpec).Should().BeGreaterThan(gate);
        code.IndexOf(CommandNames.PhaseSequence).Should().BeGreaterThan(gate);
    }

    [Fact]
    public void TheGate_SpendsNoModelCall()
    {
        CommandModelUse.For(CommandNames.RunPreflight).Use.Should().Be(ModelUse.None);
    }
}
