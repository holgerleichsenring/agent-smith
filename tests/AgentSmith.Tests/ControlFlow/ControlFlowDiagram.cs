using System.Globalization;

namespace AgentSmith.Tests.ControlFlow;

/// <summary>
/// p0408: renders the control-flow diagram from the presets. The output is the committed
/// <c>docs/assets/control-flow.svg</c>; ControlFlowDiagramTests regenerates it and
/// fails on any difference, so the picture cannot outlive the flow it describes.
/// p0482 moved it out of the website: it is a reference poster, and a landing-page
/// visitor is not the reader it was drawn for. <see cref="ArtifactPath"/> is the one
/// place that location lives, so moving it again is a one-line change.
/// </summary>
internal static class ControlFlowDiagram
{
    public const string ArtifactPath = "docs/assets/control-flow.svg";

    private const string Header =
        """
        <!-- Auto-generated from AgentSmith.Contracts.Commands.PipelinePresets by
             tests/AgentSmith.Tests/ControlFlow/ControlFlowDiagram.cs.
             Do not hand-edit. Change the presets, then regenerate by running the
             ControlFlowDiagram test with AGENTSMITH_WRITE_DIAGRAM=1. -->
        """;

    public static string Render()
    {
        var flows = ControlFlowFacts.All();
        var spine = flows[0];
        var svg = new SvgCanvas();

        var y = Title(svg, flows);
        y = ControlFlowSpine.Render(svg, spine, y + 12);
        y = ControlFlowSpine.Asks(svg, spine, y + 30);
        y = ControlFlowPresetRows.Render(svg, flows, y + 34);
        y = Legend(svg, y + 6);
        y = Limits(svg, y + 26);

        var width = ControlFlowLayout.Width.ToString("0.##", CultureInfo.InvariantCulture);
        var height = (y + ControlFlowLayout.Margin).ToString("0.##", CultureInfo.InvariantCulture);
        return $"""
                {Header}
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" role="img" aria-labelledby="cf-title cf-desc">
                  <title id="cf-title">What runs during an agent-smith run, in order</title>
                  <desc id="cf-desc">{Description(spine)}</desc>
                {ControlFlowStyle.Block}
                  <rect width="{width}" height="{height}" rx="14" class="cf-bg"/>
                {svg}</svg>

                """;
    }

    private static string Description(PresetFlow spine) =>
        $"Generated from the pipeline presets. The {spine.Name} pipeline runs {spine.Steps.Count} steps in a fixed order; "
        + $"{spine.ModelStepCount} of them call a model and the rest are deterministic machinery. "
        + "A block of steps repeats once per derived phase spec. Every other preset is listed with its master, "
        + "its step count and one tick per step.";

    private static double Title(SvgCanvas svg, IReadOnlyList<PresetFlow> flows)
    {
        var x = ControlFlowLayout.Margin;
        svg.Text(x, 46, "cf-tag", 10.5, "AGENT-SMITH · CONTROL FLOW", weight: "600");
        svg.Text(x, 76, "cf-text", 22, "What runs, in what order, and where the model gets a say", weight: "600");
        svg.Text(x, 98, "cf-mute", 11,
            $"Generated from the {flows.Count} pipeline presets in the source — a test regenerates this file and fails when the code and the picture disagree.");
        return 110;
    }

    private static double Legend(SvgCanvas svg, double y)
    {
        ControlFlowLayout.Swatch(svg, ControlFlowLayout.Margin, y,
            "cf-model", "a model runs here — the named master or prompt answers");
        ControlFlowLayout.Swatch(svg, ControlFlowLayout.Margin + 400, y,
            "cf-gate", "a gate — visible in a run only when it has a finding");
        ControlFlowLayout.Swatch(svg, ControlFlowLayout.Margin, y + 22,
            "cf-surface", "deterministic machinery — no model involved");
        return y + 32;
    }

    private static double Limits(SvgCanvas svg, double y)
    {
        svg.Text(ControlFlowLayout.Margin, y, "cf-tag", 10.5, "WHAT THIS PICTURE CANNOT SHOW", weight: "600");
        var lines = new[]
        {
            "Inside a loop box the model drives tools until it is done — the number of iterations, the files it reads and the sub-agents it spawns are run-time decisions.",
            "How many phase specs a ticket becomes, and therefore how often the phase block repeats, is decided by the derivation on the day.",
            "Master bodies and their output schemas live in a separately versioned skills catalog the operator pins; this names the master a run loads, not what that master says.",
            "Steps can skip themselves at run time (no DAST configured, a single repo, nothing to park), and an operator's YAML may override a preset's step list.",
            "Fan-out steps create work that is not in the preset: init-project's dispatch runs one project-bootstrap round per discovered component, decided per repository.",
        };
        for (var i = 0; i < lines.Length; i++)
            svg.Text(ControlFlowLayout.Margin, y + 20 + i * 17, "cf-mute", 10, "— " + lines[i]);
        return y + 20 + lines.Length * 17;
    }
}
