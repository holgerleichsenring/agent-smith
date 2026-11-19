using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-11-19-6b0ba: a run whose current spec is the ticket's stand-in, for the prompt blocks it reads.</summary>
internal static class StandInSets
{
    internal static readonly SpecDraft Draft = new("2026-11-19-0a0aa", "Widget storage", """
        spec: 2026-11-19-0a0aa
        goal: Widget storage
        done:
          - "the table exists"
        """, ["the table exists"]);

    /// <summary>Publishes a set holding <paramref name="draft"/>, a stand-in when <paramref name="standIn"/>.</summary>
    internal static PipelineContext Run(PipelineContext pipeline, SpecDraft draft, bool standIn = true)
    {
        pipeline.Set(ContextKeys.SpecSet, new SpecSet(
            "azdo-42", [new DerivedSpec(draft, "widget-storage", string.Empty, [])], SpecAccounting.Empty,
            [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)], SpecSource.Derived)
        {
            StandIns = standIn ? [draft.SpecId] : [],
        });
        pipeline.Set(ContextKeys.CurrentSpec, draft);
        return pipeline;
    }
}
