using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Validation;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-06-03c7b: a spec names its id under <c>spec:</c> and nothing else. A draft still
/// saying <c>phase:</c> is not read under an alias — the schema refuses it and the reader
/// will not invent an id from it.
/// </summary>
public sealed class SpecDraftReaderSpecKeyTests
{
    private const string PhaseKeyed = "phase: p9999\ngoal: \"g\"\n";

    [Fact]
    public void SpecDraftReader_PhaseKey_Refused()
    {
        new SpecDraftValidator(new SpecSchemaProvider()).ValidateYaml(PhaseKeyed)
            .Should().BeOfType<SpecDraftInvalid>();
        var read = () => new SpecDraftReader().Read(PhaseKeyed);
        read.Should().Throw<InvalidOperationException>().WithMessage("*'spec'*");
    }

    [Fact]
    public void SpecDraftReader_SpecKey_ReadsTheId() =>
        new SpecDraftReader().Read("spec: p9999\ngoal: \"g\"\n").PhaseId.Should().Be("p9999");
}
