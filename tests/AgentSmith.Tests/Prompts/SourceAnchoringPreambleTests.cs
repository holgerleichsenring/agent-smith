using AgentSmith.Application.Services.Prompts;
using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

public sealed class SourceAnchoringPreambleTests
{
    [Fact]
    public void Build_NamesTheAvailableTools()
    {
        var preamble = new SourceAnchoringPreamble();

        var text = preamble.Build();

        text.Should().Contain("read_file")
            .And.Contain("grep")
            .And.Contain("list_files")
            .And.Contain("run_command");
    }

    [Fact]
    public void Build_StatesAnalyzedFromSourceRequirement()
    {
        var preamble = new SourceAnchoringPreamble();

        var text = preamble.Build();

        text.Should().Contain("analyzed_from_source")
            .And.Contain("drops");
    }

    [Fact]
    public void Build_AcknowledgesOtherEvidenceModesAreLegitimate()
    {
        var preamble = new SourceAnchoringPreamble();

        var text = preamble.Build();

        text.Should().Contain("potential").And.Contain("confirmed");
        text.Should().Contain("api_path").And.Contain("template_id");
    }
}
