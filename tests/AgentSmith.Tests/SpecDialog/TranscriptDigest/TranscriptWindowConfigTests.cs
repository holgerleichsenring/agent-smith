using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.TranscriptDigest.DigestTurns;

namespace AgentSmith.Tests.SpecDialog.TranscriptDigest;

/// <summary>2026-11-12-cd4cl: the window's bounds are configuration — the prompt and the fold read the ones given.</summary>
public sealed class TranscriptWindowConfigTests
{
    private static readonly IReadOnlyList<SpecDialogTurn> Turns =
        [U("need a widget"), A(Big("OLD", 5_000)), Digest("THE-DIGEST", 1), A("later"), U("now")];

    [Fact]
    public void TranscriptWindow_Window_FromConfig_FoldsAtItsBound()
    {
        var turns = Enumerable.Range(0, 6).Select(i => U(Big($"T{i}", 3_000))).ToList();
        var narrow = new TranscriptWindowConfig { VerbatimChars = 10_000, KeepRecentChars = 7_000 };

        TranscriptWindow.Fold(turns, new TranscriptWindowConfig()).Should().BeNull("18,000 characters are under 60,000");
        var fold = TranscriptWindow.Fold(turns, narrow)!;
        Width(turns.Skip(fold.Through + 1)).Should().BeLessThanOrEqualTo(narrow.KeepRecentChars);
    }

    [Fact]
    public void DialogTranscriptSection_UsesTheSeededWindow()
    {
        var pipeline = new PipelineContext();
        DialogTranscriptSection.Render(pipeline, Turns, stewarded: false).Should().Contain("OLD", "under the default bound");

        pipeline.Set(ContextKeys.SpecDialogTranscriptWindow, new TranscriptWindowConfig { VerbatimChars = 1_000 });

        DialogTranscriptSection.Render(pipeline, Turns, stewarded: false).Should().Contain("THE-DIGEST").And.NotContain("OLD");
    }
}
