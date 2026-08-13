using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// p0413: renders the shape the scope classifier stated for this ticket as a
/// prompt section for the derivation — the input its cut-sizing rule acts on.
/// The section states the shape and the classifier's one-line reason and nothing
/// else: what the shape MEANS for the cut is a rule of the derivation master, not
/// a per-run instruction assembled here.
/// <para>
/// Empty string when no shape was stated, so the derivation then reads exactly
/// the prompt it read before the shape existed.
/// </para>
/// </summary>
public static class WorkShapePromptSection
{
    public static string Render(WorkShapeVerdict? shape) =>
        shape is null
            ? string.Empty
            : $"""

              ## The shape of this work
              Classified as: {shape.Name}{Because(shape)}
              Apply your cut-sizing rule for this shape.
              """;

    private static string Because(WorkShapeVerdict shape) =>
        string.IsNullOrWhiteSpace(shape.Reason) ? string.Empty : $" — {shape.Reason}";
}
