using AgentSmith.Contracts.Commands;

namespace AgentSmith.Tests.ControlFlow;

/// <summary>
/// p0408: the geometry and the one box the whole diagram is made of. Kept apart from the
/// sections so a layout change is one file, and the sections stay about meaning.
/// </summary>
internal static class ControlFlowLayout
{
    public const double Width = 920;
    public const double Margin = 40;
    public const double BoxWidth = 198;
    public const double BoxHeight = 58;
    public const double GapX = 16;
    public const double GapY = 16;
    public const int PerRow = 4;

    public static double ColumnX(int column) => Margin + column * (BoxWidth + GapX);

    /// <summary>Draws one step: its position in the run, its label, the beat it belongs
    /// to, and — when a model runs — who answers. Deterministic steps carry no actor,
    /// which is the point: the box says machinery, not judgement.</summary>
    public static void Step(SvgCanvas svg, double x, double y, int index, StepFact step, string actor)
    {
        svg.Rect(x, y, BoxWidth, BoxHeight, FillClass(step));
        svg.Text(x + 12, y + 20, "cf-mono-mute", 9, index.ToString("00"));
        svg.Text(x + BoxWidth - 12, y + 20, "cf-mono-mute", 8, Beat(step), anchor: "end");
        svg.Text(x + 12, y + 37, "cf-text", 11, SvgCanvas.Fit(step.Label, 32), weight: "600");

        if (step.Model.Use == ModelUse.None)
        {
            if (step.Class == CommandStepClasses.Gate)
                svg.Text(x + 12, y + 50, "cf-mono-mute", 8.5, "gate");
            return;
        }

        var mark = step.Model.Use == ModelUse.Loop ? "loop" : "call";
        svg.Text(x + 12, y + 50, "cf-accent", 8.5, $"{mark} · {SvgCanvas.Fit(actor, 28)}");
    }

    /// <summary>A legend entry: the swatch and the sentence that says what it means.</summary>
    public static void Swatch(SvgCanvas svg, double x, double y, string fillClass, string label)
    {
        svg.Rect(x, y - 9, 18, 12, fillClass, rx: 3);
        svg.Text(x + 26, y, "cf-mute", 10, label);
    }

    private static string FillClass(StepFact step) =>
        step.Model.Use != ModelUse.None ? "cf-model"
        : step.Class == CommandStepClasses.Gate ? "cf-gate"
        : "cf-surface";

    private static string Beat(StepFact step) => step.Beat.ToString().ToLowerInvariant();
}
