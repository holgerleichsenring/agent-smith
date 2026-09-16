using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-09-15-9b3e: the template rules ran only on the WRITE, so a broken binding reached
/// the operator as a 400 after Save rather than as a finding on the field that caused it.
/// <para>
/// The case that decides the shape is the LAST one: the form re-posts the whole draft on
/// every keystroke and Add appends an empty binding, so a check that judged unfinished rows
/// would report "names unknown project ''" on a row whose own badge already says it is
/// unfinished.
/// </para>
/// </summary>
public sealed class ProjectTemplateDraftCheckTests
{
    private static readonly ProjectEntity Target = Project("refapp", ["reference"]);

    [Fact]
    public void DraftCheck_TemplateNamesUnknownProject_IsAFinding()
    {
        var draft = WithTemplate(new TemplateReference("server", "nosuchproject", "reference", "server"));

        Messages(draft).Should().ContainSingle()
            .Which.Should().Contain("unknown project 'nosuchproject'");
    }

    [Fact]
    public void DraftCheck_TemplateRepoNotCarriedByThatProject_IsAFinding()
    {
        var draft = WithTemplate(new TemplateReference("server", "refapp", "elsewhere", "server"));

        Messages(draft).Should().ContainSingle()
            .Which.Should().Contain("does not carry");
    }

    [Fact]
    public void DraftCheck_TemplateNamesAGlobRepo_IsAFinding()
    {
        var draft = WithTemplate(new TemplateReference("server", "refapp", "conn/Pre*", "server"));

        Messages(draft).Should().ContainSingle().Which.Should().Contain("glob repo ref");
    }

    [Fact]
    public void TemplateRules_LocalRepoNotCarriedByThisProject_IsRefused()
    {
        // 2026-09-16-4df5: the likeliest typo now that the field exists — it is the one repo
        // ref an operator writes without the target project in front of them.
        var draft = WithTemplate(
            new TemplateReference("server", "refapp", "reference", "server", null, "nosuchrepo"));

        Messages(draft).Should().ContainSingle()
            .Which.Should().Contain("this project does not carry");
    }

    [Fact]
    public void TemplateRules_LocalRepoThisProjectCarries_IsSound() =>
        Messages(WithTemplate(
                new TemplateReference("server", "refapp", "reference", "server", null, "target")))
            .Should().BeEmpty();

    [Fact]
    public void DraftCheck_SoundTemplate_IsNotAFinding() =>
        Messages(WithTemplate(new TemplateReference("server", "refapp", "reference", "server")))
            .Should().BeEmpty();

    [Fact]
    public void DraftCheck_TemplateBindingStillEmpty_IsNotAFinding()
    {
        // What Add appends, and what the operator is halfway through typing.
        var draft = WithTemplate(new TemplateReference(string.Empty, string.Empty, string.Empty, string.Empty));

        Messages(draft).Should().BeEmpty(
            "an unfinished binding is not a broken one, and the row already says it is unfinished");
    }

    [Fact]
    public void DraftCheck_TemplateHalfTyped_IsNotAFinding()
    {
        var draft = WithTemplate(new TemplateReference("server", "refapp", string.Empty, string.Empty));

        Messages(draft).Should().BeEmpty(
            "judging a row mid-keystroke reports a project that is merely not typed yet");
    }

    [Fact]
    public void DraftCheck_NewProjectPointingAtItself_IsACycle()
    {
        // The draft is not in the stored catalog yet, so its own id has to be injected or a
        // self-reference walks against nothing and passes.
        var draft = Project("app", ["reference"]) with
        {
            Templates = [new TemplateReference("server", "app", "reference", "server")],
        };

        Messages(draft).Should().ContainSingle().Which.Should().Contain("cycle");
    }

    private static IReadOnlyList<string> Messages(ProjectEntity draft) =>
        ProjectTemplateDraftCheck.MessagesFor(draft, CatalogWith(Target));

    private static ProjectEntity WithTemplate(TemplateReference template) =>
        Project("app", ["target"]) with { Templates = [template] };

    private static ProjectEntity Project(string id, IReadOnlyList<string> repos) =>
        new(id, "agent", "tracker", repos, "code", ["code"]);

    private static ConfigCatalog CatalogWith(ProjectEntity project) =>
        new(Agents: [], Trackers: [], Repos: [], Projects: [project],
            McpServers: [], Secrets: [], Connections: []);
}
