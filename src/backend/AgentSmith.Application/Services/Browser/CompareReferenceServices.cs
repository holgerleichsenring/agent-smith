namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283di: the process-wide services one compare_reference call composes, beside the
/// render's own — the comparison run, the exact comparer, and the run's record.
/// </summary>
public sealed record CompareReferenceServices(
    RenderReferenceServices Render,
    ReferenceComparer Comparer,
    StyleDifferenceComparer Differences,
    VisualComparisonRecorder Recorder);
