using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Resume;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecNaming;

/// <summary>
/// 2026-10-12-357a: the checkpointed members 967aa's reachability list missed — declined
/// criteria, per-spec and per-run accounts, a seeded ticket's divergence — and the renamed
/// active-step key, written by the write-new release, resume on this release unchanged.
/// </summary>
public sealed class SpecNamedCheckpointMembersReadTests
{
    private const string DivergenceKey = "SeededTicketDivergence";

    private readonly PipelineContextSerializer _sut = new(NullLogger<PipelineContextSerializer>.Instance);

    [Fact]
    public void Checkpoint_SpecNamedDeclinedAndAccounts_Restore()
    {
        var context = new PipelineContext();
        context.Set(ContextKeys.DeclinedCriteria, new DeclinedCriteria([new DeclinedCriterion("c", "r", "s1a")]));
        context.Set(ContextKeys.RunAccounts, new RunAccounts([new PhaseAccounts("s1a", [])]));
        var restored = new PipelineContext();

        _sut.Restore(RenameInsideValues(_sut.Serialize(context)), restored);

        restored.TryGet<DeclinedCriteria>(ContextKeys.DeclinedCriteria, out var declined).Should().BeTrue();
        declined!.All.Single().PhaseId.Should().Be("s1a");
        restored.TryGet<RunAccounts>(ContextKeys.RunAccounts, out var accounts).Should().BeTrue();
        accounts!.Phases.Single().PhaseId.Should().Be("s1a");
    }

    [Fact]
    public void Checkpoint_SpecNamedDivergence_Restores()
    {
        var context = new PipelineContext();
        context.Set(DivergenceKey, new SetDivergence(3, ["x"]));
        var restored = new PipelineContext();

        _sut.Restore(RenameInsideValues(_sut.Serialize(context)), restored);

        restored.TryGet<SetDivergence>(DivergenceKey, out var back).Should().BeTrue();
        back!.Phases.Should().Be(3);
    }

    [Fact]
    public void CheckpointRenames_ActiveStepKey_MapsToTheDeclaredKey()
    {
        CheckpointRenames.Key("ActiveStep").Should().Be(ContextKeys.ActiveStep);
    }

    private static string RenameInsideValues(string checkpoint)
    {
        var entries = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(checkpoint)!;
        foreach (var entry in entries) entry["V"] = SpecShape.Of(entry["V"]);
        return JsonSerializer.Serialize(entries);
    }
}
