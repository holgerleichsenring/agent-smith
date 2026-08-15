using AgentSmith.Contracts.Commands;

namespace AgentSmith.Tests.ControlFlow;

/// <summary>
/// p0408: the code pipeline drawn in full — every step in execution order, with the
/// per-phase block enclosed, because the phase boundary is the one structural fact a
/// flat step list hides.
/// </summary>
internal static class ControlFlowSpine
{
    public static double Render(SvgCanvas svg, PresetFlow flow, double y)
    {
        svg.Text(ControlFlowLayout.Margin, y, "cf-tag", 10.5, "1 · ONE RUN, IN ORDER", weight: "600");
        svg.Text(ControlFlowLayout.Margin, y + 22, "cf-text", 16,
            $"The `{flow.Name}` pipeline — {flow.Steps.Count} steps, read left to right, top to bottom",
            weight: "600");
        svg.Text(ControlFlowLayout.Margin, y + 40, "cf-mute", 10.5,
            "Each box is a step the executor really runs. The word under a green box is the master or prompt that answers there.");
        y += 62;

        var before = flow.Steps.TakeWhile(s => !s.InPhaseBlock).ToList();
        var block = flow.Steps.Where(s => s.InPhaseBlock).ToList();
        var after = flow.Steps.Skip(before.Count + block.Count).ToList();

        y = Rows(svg, flow, before, 1, y);
        if (block.Count > 0) y = Block(svg, flow, block, before.Count + 1, y + 8);
        return Rows(svg, flow, after, before.Count + block.Count + 1, y + 8);
    }

    private static double Block(
        SvgCanvas svg, PresetFlow flow, IReadOnlyList<StepFact> block, int firstIndex, double y)
    {
        var rows = (block.Count + ControlFlowLayout.PerRow - 1) / ControlFlowLayout.PerRow;
        var inner = rows * (ControlFlowLayout.BoxHeight + ControlFlowLayout.GapY) - ControlFlowLayout.GapY;
        svg.Rect(ControlFlowLayout.Margin - 12, y, ControlFlowLayout.Width - 2 * (ControlFlowLayout.Margin - 12),
            inner + 46, "cf-block", rx: 10);
        svg.Text(ControlFlowLayout.Margin, y + 20, "cf-accent", 9.5,
            "PER PHASE SPEC · this block repeats once per derived phase — how many phases is decided at run time");
        Rows(svg, flow, block, firstIndex, y + 32);
        return y + inner + 46;
    }

    private static double Rows(
        SvgCanvas svg, PresetFlow flow, IReadOnlyList<StepFact> steps, int firstIndex, double y)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            var column = i % ControlFlowLayout.PerRow;
            var x = ControlFlowLayout.ColumnX(column);
            var rowY = y + i / ControlFlowLayout.PerRow * (ControlFlowLayout.BoxHeight + ControlFlowLayout.GapY);
            ControlFlowLayout.Step(svg, x, rowY, firstIndex + i, steps[i], flow.ActorOf(steps[i]));
            if (column < ControlFlowLayout.PerRow - 1 && i < steps.Count - 1)
                svg.Line(x + ControlFlowLayout.BoxWidth, rowY + ControlFlowLayout.BoxHeight / 2,
                    x + ControlFlowLayout.BoxWidth + ControlFlowLayout.GapX,
                    rowY + ControlFlowLayout.BoxHeight / 2, "cf-flow");
        }

        var rows = (steps.Count + ControlFlowLayout.PerRow - 1) / ControlFlowLayout.PerRow;
        return y + rows * (ControlFlowLayout.BoxHeight + ControlFlowLayout.GapY);
    }

    /// <summary>
    /// What agent-smith asks a model for on this run, and what it does with the answer.
    /// The answer column is what THIS repo parses; for the master loop it is the master's
    /// own declared output_schema, which lives in the pinned skills catalog.
    /// </summary>
    public static double Asks(SvgCanvas svg, PresetFlow flow, double y)
    {
        svg.Text(ControlFlowLayout.Margin, y, "cf-tag", 10.5, "2 · WHAT THE MODEL IS ASKED FOR", weight: "600");
        svg.Text(ControlFlowLayout.Margin, y + 20, "cf-mute", 10.5,
            "Every model call in the run above. Everything not listed here is deterministic machinery.");
        y += 38;
        svg.Line(ControlFlowLayout.Margin, y, ControlFlowLayout.Width - ControlFlowLayout.Margin, y, "cf-rule");
        y += 18;

        foreach (var step in flow.Steps.Where(s => s.Model.Use != ModelUse.None))
        {
            svg.Text(ControlFlowLayout.Margin, y, "cf-text", 11, step.Label, weight: "600");
            svg.Text(ControlFlowLayout.Margin + 210, y, "cf-accent", 10, flow.ActorOf(step));
            svg.Text(ControlFlowLayout.Margin + 440, y, "cf-mute", 10.5, $"→ {step.Model.Answer}");
            y += 24;
        }

        return y;
    }
}
