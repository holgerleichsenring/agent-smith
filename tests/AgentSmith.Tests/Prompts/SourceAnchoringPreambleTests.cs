using AgentSmith.Application.Services.Prompts;
using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

public sealed class SourceAnchoringPreambleTests
{
    [Fact]
    public void Build_NamesTheFullToolSurface()
    {
        var preamble = new SourceAnchoringPreamble();

        var text = preamble.Build();

        text.Should().Contain("read_file")
            .And.Contain("grep")
            .And.Contain("glob")
            .And.Contain("list_files")
            .And.Contain("edit")
            .And.Contain("write_file")
            .And.Contain("run_command")
            .And.Contain("http_request");
    }

    [Fact]
    public void Build_StatesDowngradeNotDropForAnalyzedFromSource()
    {
        // Post-PR #171: validator downgrades, does NOT drop. The preamble must
        // reflect the new contract so the LLM understands its safety net.
        var preamble = new SourceAnchoringPreamble();

        var text = preamble.Build();

        text.Should().Contain("analyzed_from_source")
            .And.Contain("downgrades");
        text.Should().NotContain("drops any analyzed_from_source");
    }

    [Fact]
    public void Build_AcknowledgesOtherEvidenceModesAreLegitimate()
    {
        var preamble = new SourceAnchoringPreamble();

        var text = preamble.Build();

        text.Should().Contain("potential").And.Contain("confirmed");
        text.Should().Contain("api_path").And.Contain("template_id");
    }

    [Fact]
    public void Build_AffirmsPotentialWithoutAnchorIsValid()
    {
        // Absence-of-config findings ("no UseHsts anywhere") must be a
        // first-class evidence_mode=potential observation without a file anchor.
        var preamble = new SourceAnchoringPreamble();

        var text = preamble.Build();

        text.Should().Contain("absence");
        text.Should().Contain("file to null");
    }
}
