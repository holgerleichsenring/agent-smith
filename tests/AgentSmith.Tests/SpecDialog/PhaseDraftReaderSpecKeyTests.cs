using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Validation;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-06-03c7b: a spec names its id under <c>spec:</c> and nothing else. A draft still
/// saying <c>phase:</c> is not read under an alias — the schema refuses it and the reader
/// will not invent an id from it.
/// </summary>
public sealed class PhaseDraftReaderSpecKeyTests
{
    private const string PhaseKeyed = "phase: p9999\ngoal: \"g\"\n";

    [Fact]
    public void PhaseDraftReader_PhaseKey_Refused()
    {
        new SpecDraftValidator(new PhaseSpecSchemaProvider()).ValidateYaml(PhaseKeyed)
            .Should().BeOfType<SpecDraftInvalid>();
        var read = () => new PhaseDraftReader().Read(PhaseKeyed);
        read.Should().Throw<InvalidOperationException>().WithMessage("*'spec'*");
    }

    [Fact]
    public void PhaseDraftReader_SpecKey_ReadsTheId() =>
        new PhaseDraftReader().Read("spec: p9999\ngoal: \"g\"\n").PhaseId.Should().Be("p9999");
}
