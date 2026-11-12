using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-11-11-205fi: an upload's coverage changes when a turn opens files, so it stands in the design turn's user
/// prompt; the system prompt carries only the address list, which changes on upload. 2026-11-12-cd4ck: no per-upload note.
/// </summary>
public sealed class DesignCoveragePlacementTests
{
    private readonly ReferenceSandboxFixture _fixture = new();

    [Fact]
    public void DesignTurn_SecondTurnOpensFiles_SystemPromptUnchanged()
    {
        var addresses = new[] { "repo-a", "reference:app" };
        var first = Map(Upload(opened: []));
        var second = Map(Upload(opened: ["app/a.cs"]));

        ReferencePromptSection.Build(addresses).Should().Be(ReferencePromptSection.Build(addresses),
            "the system prompt's upload section reads the addresses only")
            .And.NotContain("Earlier turns").And.Contain("`reference:app`");
        ReferencePromptSection.ThisTurn(first).Should().NotBe(ReferencePromptSection.ThisTurn(second),
            "what a turn opened moves to the turn's own prompt");
    }

    [Fact]
    public void DesignTurn_Coverage_RendersInUserPromptUnderItsAddress()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript, [new SpecDialogTurn("user", "go on")]);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            Map(Upload(opened: ["app/a.cs"])));

        var prompt = new SpecDialogPromptFactory().Build(pipeline, 0, 0);

        prompt.Should().Contain("## Uploads at this turn\n- `reference:app`")
            .And.Contain("Earlier turns of this conversation opened 1 of the 2 files");
    }

    private ISandbox Upload(string[] opened) => _fixture.Factory(Holds.None()).Create(
        ReferenceSandboxFixture.Project, ReferenceSandboxFixture.Conversation, "reference:app", "set-1",
        coverage: new ReferenceCoverage(new HashSet<string> { "app/a.cs", "app/b.cs" }, opened.ToHashSet(), 0));

    private static Dictionary<string, ISandbox> Map(ISandbox upload) => new() { ["reference:app"] = upload };
}
