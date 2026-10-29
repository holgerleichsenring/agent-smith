using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// 2026-10-29-6bd3a: a per-project trigger block is moved into what the studio shows — the project's
/// resolution and its tracker — and only where nothing is set there. Read from YAML, so the block's
/// keys bind the way an operator's file binds them.
/// </summary>
public sealed class LegacyTriggerFoldTests
{
    private const string Tracker = """
        trackers:
          board:
            type: jira
            open_states: [To Do, In Progress]
            failed_status: In Review
        """;

    [Fact]
    public void Fold_ProjectWithResolution_KeepsItsResolution()
    {
        var raw = Fold(Tracker + "\n" + """
            projects:
              alpha:
                tracker: board
                resolution: { tag: "repository:alpha" }
                jira_trigger:
                  project_resolution: { strategy: tag, value: alpha }
            """);

        raw.Projects["alpha"].Resolution.Should().Equal(new Dictionary<string, string> { ["tag"] = "repository:alpha" });
        raw.Projects["alpha"].LegacyJiraTrigger.Should().BeNull();
    }

    [Fact]
    public void Fold_ProjectWithoutResolution_TakesTheBlocksResolution()
    {
        var raw = Fold(Tracker + "\n" + """
            projects:
              alpha:
                tracker: board
                jira_trigger:
                  project_resolution: { strategy: area_path, value: 'Team\Alpha' }
            """);

        raw.Projects["alpha"].Resolution.Should().Equal(new Dictionary<string, string> { ["area_path"] = @"Team\Alpha" });
    }

    [Fact]
    public void Fold_TrackerWithOpenStates_KeepsItsStatuses()
    {
        var raw = Fold(Tracker + "\n" + """
            projects:
              alpha:
                tracker: board
                jira_trigger:
                  trigger_statuses: [To Do]
                  failed_status: Blocked
            """);

        raw.Trackers["board"].TriggerStatuses.Should().BeEmpty("the tracker's open states are the gate the studio shows");
        raw.Trackers["board"].FailedStatus.Should().Be("In Review");
    }

    [Fact]
    public void Fold_UnsetTrackerField_TakesTheBlocksValue()
    {
        var raw = Fold(Tracker + "\n" + """
            projects:
              alpha:
                tracker: board
                jira_trigger:
                  comment_keyword: "@agent-smith"
                  done_status: Done
                  default_pipeline: code
            """);

        raw.Trackers["board"].CommentKeyword.Should().Be("@agent-smith");
        raw.Trackers["board"].DoneStatus.Should().Be("Done");
        raw.Trackers["board"].DefaultPipeline.Should().Be("code");
    }

    [Fact]
    public void Fold_UndeclaredBlockField_TakesNothing()
    {
        // The trigger classes initialise trigger_statuses, done_status and assignee_name; a block
        // read through them would have filled all three without declaring any.
        var raw = Fold("""
            trackers:
              board: { type: jira }
            projects:
              alpha:
                tracker: board
                jira_trigger: { comment_keyword: go }
            """);

        raw.Trackers["board"].TriggerStatuses.Should().BeEmpty();
        raw.Trackers["board"].DoneStatus.Should().BeNull();
        raw.Trackers["board"].AssigneeName.Should().BeNull();
    }

    [Fact]
    public void Fold_JiraBlock_MovesAssigneeAndSecretToTheTracker()
    {
        var raw = Fold(Tracker + "\n" + """
            projects:
              alpha:
                tracker: board
                jira_trigger:
                  assignee_name: Bot
                  secret: ${JIRA_WEBHOOK_SECRET}
            """);

        raw.Trackers["board"].AssigneeName.Should().Be("Bot");
        raw.Trackers["board"].WebhookSecret.Should().Be("${JIRA_WEBHOOK_SECRET}");
    }

    [Fact]
    public void Fold_TwoProjectsDisagree_FirstByNameWinsAndTheOtherIsAFinding()
    {
        var (raw, findings) = FoldWithFindings(Tracker + "\n" + """
            projects:
              beta:
                tracker: board
                jira_trigger: { assignee_name: Other }
              alpha:
                tracker: board
                jira_trigger: { assignee_name: Bot }
            """);

        raw.Trackers["board"].AssigneeName.Should().Be("Bot");
        findings.Should().ContainSingle(f => f.Project == "beta" && f.Field!.EndsWith("assignee_name"))
            .Which.Reason.Should().Contain("alpha");
    }

    [Fact]
    public void Fold_BlockOfAnotherKind_IsDroppedWithAFinding()
    {
        var (raw, findings) = FoldWithFindings(Tracker + "\n" + """
            projects:
              alpha:
                tracker: board
                github_trigger: { comment_keyword: go }
            """);

        raw.Projects["alpha"].LegacyGithubTrigger.Should().BeNull();
        raw.Trackers["board"].CommentKeyword.Should().BeNull();
        findings.Should().ContainSingle(f => f.Project == "alpha" && f.Trigger == "github_trigger");
    }

    [Fact]
    public void Fold_FailedStatusThatIsATrackerTriggerStatus_IsNotTaken()
    {
        var (raw, findings) = FoldWithFindings("""
            trackers:
              board: { type: jira, open_states: [To Do, In Progress] }
            projects:
              alpha:
                tracker: board
                jira_trigger: { failed_status: In Progress }
            """);

        raw.Trackers["board"].FailedStatus.Should().BeNull(
            "on the tracker it would re-claim every ticket it parks, for every project of that tracker");
        findings.Should().ContainSingle(f => f.Field!.EndsWith("failed_status"));
    }

    private static RawAgentSmithConfig Fold(string yaml) => FoldWithFindings(yaml).Raw;

    private static (RawAgentSmithConfig Raw, IReadOnlyList<AgentSmith.Contracts.Models.Configuration.StartupFinding> Findings)
        FoldWithFindings(string yaml)
    {
        var raw = new RawConfigYaml().Deserialize(yaml);
        var findings = new LegacyTriggerFold().Apply(raw);
        return (raw, findings);
    }
}
