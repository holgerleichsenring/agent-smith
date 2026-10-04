using AgentSmith.Application.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-10-04-2bf2: the operator-owned tail of principles.md — the last "## Project Specifics"
/// section through the end of the file — is what a refresh copies verbatim.
/// </summary>
public sealed class ProjectSpecificsSectionTests
{
    private const string Composed =
        "# Coding Principles\n\nNEW CORE\n\n---\n\n## Project Specifics (ratified additions)\n\n_None yet._\n";

    [Fact]
    public void ProjectSpecificsSection_FileWithoutTheHeading_KeepsNothing()
    {
        ProjectSpecificsSection.Extract("# Coding Principles\n\n### Project Specifics are elsewhere\n")
            .Should().BeNull("only a level-two heading at the start of a line is the section");
    }

    [Fact]
    public void ProjectSpecificsSection_TwoHeadings_ExtractsTheLastThroughTheEnd()
    {
        const string file = "## Project Specifics\nold\n\n## Project Specifics (ratified additions)\n- rule A\n";

        ProjectSpecificsSection.Extract(file)
            .Should().Be("## Project Specifics (ratified additions)\n- rule A\n");
    }

    [Fact]
    public void ProjectSpecificsSection_Append_ReplacesTheComposedPlaceholder()
    {
        var result = ProjectSpecificsSection.Append(Composed, "## Project Specifics (ratified additions)\n- rule A");

        result.Should().Be(
            "# Coding Principles\n\nNEW CORE\n\n---\n\n## Project Specifics (ratified additions)\n- rule A");
    }

    [Fact]
    public void ProjectSpecificsSection_AppendToACompositionWithoutTheSection_AddsItAtTheEnd()
    {
        ProjectSpecificsSection.Append("# Core\n\n", "## Project Specifics\n- rule A\n")
            .Should().Be("# Core\n\n## Project Specifics\n- rule A\n");
    }
}
