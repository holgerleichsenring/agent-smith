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
    public void Build_PullsTowardToolUseViaIdentityAndConcreteTargets()
    {
        // Post-PR #173-followup: earlier permissive wording produced 0-tool-call
        // rounds. The preamble must pull toward investigation via identity +
        // a concrete recon flow + a numeric target, not via threat.
        var preamble = new SourceAnchoringPreamble();

        var text = preamble.Build();

        text.Should().Contain("investigator");
        text.Should().Contain("read the files");
        text.Should().Contain("2-5 files");
        text.Should().Contain("primary deliverable");
    }

    [Fact]
    public void Build_DiscourageZeroToolCallShortcut()
    {
        var preamble = new SourceAnchoringPreamble();

        var text = preamble.Build();

        // The wording must NOT lean on "the framework will downgrade automatically"
        // as the only deterrent — that was the regression.
        text.Should().NotContain("downgrades unverified claims to potential automatically");
        // The wording MUST explicitly name the failure mode.
        text.Should().Contain("zero tool calls");
        text.Should().Contain("shallow");
    }

    [Fact]
    public void Build_AcknowledgesOtherEvidenceModesAreLegitimate()
    {
        // The preamble names the three evidence_modes; concrete anchor types
        // (api_path / schema_name / template_id) are catalog-specific and live
        // in per-skill SKILL.md guidance, not in this universal text.
        var preamble = new SourceAnchoringPreamble();

        var text = preamble.Build();

        text.Should().Contain("potential")
            .And.Contain("confirmed")
            .And.Contain("analyzed_from_source");
    }

    [Fact]
    public void Build_AffirmsPotentialWithoutAnchorIsValid()
    {
        // Absence-of-config findings ("no UseHsts anywhere") must be a
        // first-class evidence_mode=potential observation without a file anchor.
        var preamble = new SourceAnchoringPreamble();

        var text = preamble.Build();

        text.Should().Contain("absence");
        text.Should().Contain("file null");
    }

    [Fact]
    public void Build_RequiresHttpRequestForConfirmedEvidence()
    {
        var preamble = new SourceAnchoringPreamble();

        var text = preamble.Build();

        text.Should().Contain("confirmed");
        text.Should().Contain("http_request");
    }
}
