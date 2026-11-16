namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-11-16-f3fba: what a refusal is about and the setting that lifts it, as data, so the ticket can
/// name the image or the repositories and the control instead of quoting the run's summary.
/// </summary>
/// <param name="Subject">What the refusal is about, e.g. "Image node:22-bookworm (context default)".</param>
/// <param name="Repos">The repositories it concerns; empty when it concerns none in particular.</param>
/// <param name="Control">The setting to change, as the settings page names it; null when no setting helps.</param>
/// <param name="SetTo">The value that lifts the refusal; null when no setting helps.</param>
/// <param name="SettingsPath">The dashboard page that holds the control; null when there is none.</param>
public sealed record EndDetail(
    string Subject,
    IReadOnlyList<string> Repos,
    string? Control = null,
    string? SetTo = null,
    string? SettingsPath = null);
