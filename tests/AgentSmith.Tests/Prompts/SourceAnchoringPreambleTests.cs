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
            .And.Contain("grep_in_file")
            .And.Contain("grep_in_tree")
            .And.Contain("find_files")
            .And.Contain("list_directory")
            .And.Contain("directory_tree")
            .And.Contain("edit")
            .And.Contain("multi_edit")
            .And.Contain("write_file")
            .And.Contain("run_command")
            .And.Contain("http_request");
    }

    [Fact]
    public void Build_DoesNotNameDeprecatedAliases()
    {
        // p0153: deprecated grep / glob / list_files stay registered as forwarders
        // but the preamble must steer the LLM at the new names. p0154 removes the
        // aliases.
        var preamble = new SourceAnchoringPreamble();

        var text = preamble.Build();

        text.Should().NotMatchRegex(@"\bglob\b");
        text.Should().NotMatchRegex(@"\blist_files\b");
        // 'grep' is part of 'grep_in_file' / 'grep_in_tree' so we cannot regex on
        // it; instead assert the standalone-tool comma-separated list does not
        // mention it as its own entry.
        text.Should().NotContain(", grep, ");
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

    [Fact]
    public void Build_NoLongerContainsSubAgentRoleNamingBlock()
    {
        // p0179a: the p0177 step-11 sub-agent guidance moved into
        // coding-agent-master where spawn_agents actually lives.
        var preamble = new SourceAnchoringPreamble();

        var text = preamble.Build();

        text.Should().NotContain("Sub-agent fan-out");
        text.Should().NotContain("spawn_agents");
        text.Should().NotContain("ContextMapInvestigator");
        text.Should().NotContain("read_sub_agent_observations");
    }
}
