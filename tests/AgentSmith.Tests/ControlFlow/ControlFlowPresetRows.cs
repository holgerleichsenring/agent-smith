using AgentSmith.Contracts.Commands;

namespace AgentSmith.Tests.ControlFlow;

/// <summary>
/// p0408: every preset at a glance — its master, its length, and the shape of its run as
/// one tick per step. Same generator, same tables, so the eight rows cannot drift apart
/// from the one pipeline drawn in full above.
/// </summary>
internal static class ControlFlowPresetRows
{
    private const double TickWidth = 11;
    private const double TickPitch = 15;
    private const double StripX = 430;

    public static double Render(SvgCanvas svg, IReadOnlyList<PresetFlow> flows, double y)
    {
        svg.Text(ControlFlowLayout.Margin, y, "cf-tag", 10.5, "3 · EVERY PIPELINE THAT SHIPS", weight: "600");
        svg.Text(ControlFlowLayout.Margin, y + 20, "cf-mute", 10.5,
            "One tick per step, in order. Green = a model runs there; blue = a gate that speaks only when it has a finding.");
        y += 40;

        foreach (var flow in flows)
        {
            svg.Line(ControlFlowLayout.Margin, y - 14,
                ControlFlowLayout.Width - ControlFlowLayout.Margin, y - 14, "cf-rule");
            svg.Text(ControlFlowLayout.Margin, y + 4, "cf-mono", 12, flow.Name, weight: "600");
            svg.Text(ControlFlowLayout.Margin, y + 20, "cf-mute", 9.5,
                $"{string.Join(" + ", flow.LoopActors)} · {flow.Steps.Count} steps · {flow.ModelStepCount} with a model");
            Strip(svg, flow, y);
            y += 48;
        }

        return y;
    }

    private static void Strip(SvgCanvas svg, PresetFlow flow, double y)
    {
        for (var i = 0; i < flow.Steps.Count; i++)
            svg.Rect(StripX + i * TickPitch, y - 8, TickWidth, 18, TickClass(flow.Steps[i]), rx: 3);

        var block = flow.Steps.Select((s, i) => (s, i)).Where(t => t.s.InPhaseBlock).ToList();
        if (block.Count == 0) return;

        var from = StripX + block[0].i * TickPitch;
        var to = StripX + (block[^1].i * TickPitch) + TickWidth;
        svg.Line(from, y + 16, to, y + 16, "cf-blockline");
        svg.Text(to + 8, y + 20, "cf-accent", 9, "× one per phase");
    }

    private static string TickClass(StepFact step) =>
        step.Model.Use != ModelUse.None ? "cf-model"
        : step.Class == CommandStepClasses.Gate ? "cf-gate"
        : "cf-surface";
}
