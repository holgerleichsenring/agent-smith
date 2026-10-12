using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Resume;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecNaming;

/// <summary>
/// 2026-10-12-967aa/967ab: a parked run checkpointed by the release before the rename — its context
/// entries under phase-named keys and type names, its remaining commands under the old command
/// names — resumes on this release exactly as one this release checkpointed.
/// </summary>
public sealed class SpecNamedCheckpointReadTests
{
    private readonly PipelineContextSerializer _sut = new(NullLogger<PipelineContextSerializer>.Instance);

    [Fact]
    public void Checkpoint_RenamedTypeName_Restores()
    {
        var draft = new SpecDraft("s1", "Read both", "spec: s1", ["s0"]) { Done = ["It reads."] };
        var entries = JsonSerializer.Serialize(new[]
        {
            Entry("PhaseSpec", "AgentSmith.Contracts.Models.PhaseDraft, AgentSmith.Contracts",
                SpecShape.Of(JsonSerializer.Serialize(draft))),
        });
        var restored = new PipelineContext();

        _sut.Restore(entries, restored);

        restored.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var back).Should().BeTrue();
        back.Should().BeEquivalentTo(draft);
    }

    [Fact]
    public void Checkpoint_RenamedKeysAndGenericArgument_Restore()
    {
        var reviews = new RunSpecReviews([new SpecReviewEntry("s1", SpecReviewReport.NotTaken("cap"))]);
        var entries = JsonSerializer.Serialize(new[]
        {
            Entry("RunPhaseReviews", "AgentSmith.Domain.Models.RunPhaseReviews, AgentSmith.Domain",
                SpecShape.Of(JsonSerializer.Serialize(reviews))),
            Entry("PhaseAccounts",
                "System.Collections.Generic.List`1[[AgentSmith.Contracts.Models.PhaseDraft, AgentSmith.Contracts]], System.Private.CoreLib",
                "[]"),
        });
        var restored = new PipelineContext();

        _sut.Restore(entries, restored);

        restored.TryGet<RunSpecReviews>(ContextKeys.RunSpecReviews, out var back).Should().BeTrue();
        back!.Specs.Single().SpecId.Should().Be("s1");
        restored.TryGet<List<SpecDraft>>(ContextKeys.SpecAccounts, out _).Should().BeTrue();
    }

    [Fact]
    public void Checkpoint_SpecSetWithOldMember_Restores()
    {
        var set = ApprovedSets.Set("k", [ApprovedSets.Spec("s1a")], null);
        var context = new PipelineContext();
        context.Set(ContextKeys.SpecSet, set);
        var restored = new PipelineContext();

        _sut.Restore(RenameInsideValues(_sut.Serialize(context)), restored);

        restored.TryGet<SpecSet>(ContextKeys.SpecSet, out var back).Should().BeTrue();
        back!.Specs.Single().SpecId.Should().Be("s1a");
    }

    [Fact]
    public void Checkpoint_DeclaredNames_StillRestoreUnchanged()
    {
        var draft = new SpecDraft("p1", "g", "spec: p1", []);
        var context = new PipelineContext();
        context.Set(ContextKeys.CurrentSpec, draft);
        var restored = new PipelineContext();

        _sut.Restore(_sut.Serialize(context), restored);

        restored.Get<SpecDraft>(ContextKeys.CurrentSpec).Should().BeEquivalentTo(draft);
    }

    [Fact]
    public void RemainingCommand_OldCommandAndId_ResumesAsTheDeclaredCommand()
    {
        var written = JsonSerializer.Serialize(new List<CheckpointCommand>
        {
            new(CommandNames.SelectSpec, SpecId: "s1"),
            new(CommandNames.VerifySpec + ":api", SpecId: "s1"),
        });
        var newShape = SpecShape.Of(written)
            .Replace(CommandNames.SelectSpec, "SelectPhaseCommand")
            .Replace(CommandNames.VerifySpec, "VerifyPhaseCommand");

        var commands = JsonSerializer.Deserialize<List<CheckpointCommand>>(
                newShape, AgentSmith.Contracts.Json.AcceptedNames.DefaultOptions)!
            .Select(c => c.ToPipelineCommand()).ToList();

        commands.Select(c => c.Name).Should().Equal(CommandNames.SelectSpec, CommandNames.VerifySpec + ":api");
        commands.Should().OnlyContain(c => c.SpecId == "s1");
    }

    private static object Entry(string key, string type, string value) => new { K = key, T = type, V = value };

    // The checkpoint nests each value as an escaped JSON string; the old release named inside it.
    private static string RenameInsideValues(string checkpoint)
    {
        var entries = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(checkpoint)!;
        foreach (var entry in entries) entry["V"] = SpecShape.Of(entry["V"]);
        return JsonSerializer.Serialize(entries);
    }
}
