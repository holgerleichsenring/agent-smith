using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Models.Structure;
using FluentAssertions;

namespace AgentSmith.Tests.Structure;

public sealed class StructureLimitsTests
{
    private const string Delta = """
        # Coding Principles

        ## Limits

        ```yaml
        function_lines: 55
        file_lines: 300
        ```

        ## Project Specifics (ratified additions)
        """;

    [Fact]
    public void Limits_DeltaFence_ReadsEveryKey()
    {
        var limits = new StructureLimits().Read(Delta);

        limits.For(StructureMetric.FunctionLines).Should().Be(55);
        limits.For(StructureMetric.FileLines).Should().Be(300);
        limits.For(StructureMetric.TypeLines).Should().BeNull();
    }

    [Fact]
    public void Limits_ProjectSpecificsFence_OverridesTheDeltaKey()
    {
        var house = Delta + "\n\n### Limits\n\n```yaml\nfunction_lines: 20\ntype_lines: 120\n```\n";

        var limits = new StructureLimits().Read(house);

        limits.For(StructureMetric.FunctionLines).Should().Be(20);
        limits.For(StructureMetric.TypeLines).Should().Be(120);
        limits.For(StructureMetric.FileLines).Should().Be(300, "a key the house leaves out keeps the delta's");
    }

    [Fact]
    public void Limits_HouseHeadingOutsideProjectSpecifics_IsIgnored()
    {
        const string text = "## Additions\n\n### Limits\n\n```yaml\nfunction_lines: 5\n```\n";

        new StructureLimits().Read(text).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Limits_NoFence_SetsNoLimit()
    {
        new StructureLimits().Read("## Limits\n\nNo limits — none stated.\n\n## Artefacts\n")
            .IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Limits_UnknownKeyOrNonPositiveValue_SetsNothing()
    {
        const string text = "## Limits\n\n```yaml\nmethod_lines: 10\nfile_lines: 0\n```\n";

        new StructureLimits().Read(text).IsEmpty.Should().BeTrue();
    }
}
