using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Pocs.Rollout;

/// <summary>
/// 2026-11-23-c81ec: judges one poll. A reading counts as THIS RENDER only when the live Deployment's pod-template image
/// of container 'app' (registry, repository and digest), its variables-checksum annotation and spec.replicas equal the
/// expected state and observedGeneration has reached generation; before that it waits, whatever Argo reports about the
/// previous render (a Degraded left by it does not fail this run). On such a reading it settles on Synced + Healthy with
/// ready = updated = desired (for 0, no ready replica), and fails on Degraded or a Failed / Error operation. Never
/// status.summary.images, never Argo's sync revision: all PoCs share one branch, so it moves with other PoCs' commits.
/// </summary>
public sealed class PocRolloutVerdict
{
    public PocRolloutReading Judge(PocRolloutExpected expected, ArgoApplicationState? app, ArgoLiveDeployment? live)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (expected.Absent) return Gone(expected, app);
        if (app is null)
            return Wait($"the Application {expected.Application} does not exist yet; the ApplicationSet's generator creates it when it requeues");
        if (live is null) return Wait($"{State(app)}; the Application holds no live Deployment 'app' yet");
        var line = Line(expected, app, live);
        if (!IsThisRender(expected, live)) return Wait(line + "; not this render yet");
        if (app.HealthStatus == ArgoApplicationState.Degraded || app.OperationFailed)
            return new PocRolloutReading(PocRolloutReadingKind.Failed, line, app.HealthMessage ?? app.OperationMessage ?? app.HealthStatus);
        return IsSettled(expected, app, live) ? new PocRolloutReading(PocRolloutReadingKind.Settled, line) : Wait(line);
    }

    private static PocRolloutReading Gone(PocRolloutExpected expected, ArgoApplicationState? app) => app is null
        ? new PocRolloutReading(PocRolloutReadingKind.Settled, $"the Application {expected.Application} is gone")
        : Wait($"{State(app)}; the Application {expected.Application} still exists");

    private static bool IsThisRender(PocRolloutExpected expected, ArgoLiveDeployment live) =>
        ImageMatches(expected, live.TemplateImage) && string.Equals(live.VariablesChecksum, expected.Checksum, StringComparison.Ordinal)
        && live.Replicas == expected.Replicas && live.ObservedGeneration >= live.Generation;

    private static bool IsSettled(PocRolloutExpected expected, ArgoApplicationState app, ArgoLiveDeployment live) =>
        app.SyncStatus == ArgoApplicationState.Synced && app.HealthStatus == ArgoApplicationState.Healthy
        && (expected.Replicas > 0
            ? live.ReadyReplicas == expected.Replicas && live.UpdatedReplicas == expected.Replicas
            : live.ReadyReplicas == 0);

    // <registry>/<repository>[:<tag>]@<digest> — the shape 933de's ImageReference renders; the tag (the commit) is free.
    private static bool ImageMatches(PocRolloutExpected expected, string? image)
    {
        if (string.IsNullOrWhiteSpace(image) || expected.Digest is null) return false;
        var name = $"{expected.Registry}/{expected.Repository}";
        if (!image.StartsWith(name, StringComparison.Ordinal) || !image.EndsWith("@" + expected.Digest, StringComparison.Ordinal))
            return false;
        var between = image[name.Length..^(expected.Digest.Length + 1)];
        return between.Length == 0 || (between[0] == ':' && !between.Contains('/') && !between.Contains('@'));
    }

    private static string Line(PocRolloutExpected expected, ArgoApplicationState app, ArgoLiveDeployment live) =>
        $"{State(app)}, template image {(ImageMatches(expected, live.TemplateImage) ? "is this render's" : "is another")}, "
        + $"checksum {(string.Equals(live.VariablesChecksum, expected.Checksum, StringComparison.Ordinal) ? "matches" : "differs")}, "
        + $"generation {live.ObservedGeneration}/{live.Generation}, replicas {live.ReadyReplicas} ready / {live.UpdatedReplicas} "
        + $"updated / {live.Replicas?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"} of {expected.Replicas}";

    private static string State(ArgoApplicationState app) =>
        $"sync {app.SyncStatus ?? "unknown"}, health {app.HealthStatus ?? "unknown"}";

    private static PocRolloutReading Wait(string line) => new(PocRolloutReadingKind.Waiting, line);
}
