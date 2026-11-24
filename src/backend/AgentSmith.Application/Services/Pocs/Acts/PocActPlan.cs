using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Acts;

/// <summary>
/// 2026-11-23-c81ed: the ONE table of the lifecycle acts, read by the launcher, the PocAct step and the detail page — pure:
/// act + row -> desired state or refusal. Stop from ready or failed (0 replicas), start from stopped (1), rollback and
/// apply from ready, stopped or failed (replicas kept), each only with a Revision on the row; delete from any state but
/// deleted. A Revision null refuses every act but delete, so nothing re-renders the folder of a deleted PoC whose name a
/// new PoC has taken. The registry check of a rollback's image is the step's (<see cref="PocRollbackImage"/>).
/// </summary>
public sealed class PocActPlan
{
    private static readonly string[] Deployed = [PocStates.Ready, PocStates.Stopped, PocStates.Failed];

    /// <summary>Why <paramref name="act"/> cannot run from the row's state, or null when it may.</summary>
    public string? WhyNot(string act, PocActSubject poc)
    {
        ArgumentNullException.ThrowIfNull(poc);
        if (!PocActKinds.IsAct(act)) return $"'{act}' is not a PoC act; the acts are {string.Join(", ", PocActKinds.All)}";
        if (act == PocActKinds.Delete)
            return poc.State == PocStates.Deleted ? $"'{poc.Name}' is deleted already; delete needs any other state" : null;
        string[] from = act switch { PocActKinds.Stop => [PocStates.Ready, PocStates.Failed], PocActKinds.Start => [PocStates.Stopped], _ => Deployed };
        if (!from.Contains(poc.State)) return $"'{poc.Name}' is {poc.State}; {act} needs it {string.Join(" or ", from)}";
        return poc.Revision is null || poc.ImageDigest is null
            ? $"'{poc.Name}' has no deployed revision{(poc.IsDeletePending ? " (its delete is pending)" : string.Empty)}; {act} needs one"
            : null;
    }

    /// <summary>The desired state <paramref name="order"/> wants from <paramref name="poc"/>; a rollback reads the revision
    /// it rolls to, an apply its readiness seed bounded by pocs.readiness_path_max_chars.</summary>
    public PocActDecision Decide(PocActOrder order, PocActSubject poc, PocActRevision? target, int readinessMaxChars)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (WhyNot(order.Act, poc) is { } why) return PocActDecision.Refused(why);
        var current = new PocDesiredState(poc.Replicas, poc.Revision ?? 0, poc.ImageDigest ?? string.Empty, poc.ReadinessPath);
        return order.Act switch
        {
            PocActKinds.Delete => new PocActDecision(null, true, null),
            PocActKinds.Stop => new PocActDecision(current with { Replicas = 0 }, false, null),
            PocActKinds.Start => new PocActDecision(current with { Replicas = 1 }, false, null),
            PocActKinds.Rollback => Rollback(order, poc, current, target),
            _ => Apply(order, current, readinessMaxChars),
        };
    }

    private static PocActDecision Rollback(PocActOrder order, PocActSubject poc, PocDesiredState current, PocActRevision? target)
    {
        if (order.Revision is not { } n) return PocActDecision.Refused($"A rollback of '{poc.Name}' names the revision to roll back to");
        if (target is null || target.Number != n) return PocActDecision.Refused($"'{poc.Name}' has no revision {n}");
        if (target.RolloutState != PocRolloutStates.Healthy || target.Digest is not { } digest)
            return PocActDecision.Refused($"Revision {n} of '{poc.Name}' never rolled out healthy; a rollback goes only to a healthy revision");
        if (n == current.Revision || digest == current.ImageDigest)
            return PocActDecision.Refused($"'{poc.Name}' already runs revision {current.Revision} ({current.ImageDigest}); roll back to another one");
        return new PocActDecision(current with { Revision = n, ImageDigest = digest }, false, null);
    }

    private PocActDecision Apply(PocActOrder order, PocDesiredState current, int maxChars)
    {
        var (keep, path, refused) = Readiness(order.Readiness, maxChars);
        if (refused is not null) return PocActDecision.Refused(refused);
        return new PocActDecision(keep ? current : current with { ReadinessPath = path }, false, null);
    }

    /// <summary>The tri-state readiness seed: null keeps the row's path, 'tcp' clears it, a '/' path of at most
    /// <paramref name="maxChars"/> characters sets it; anything else is refused naming the value, the rule and the key.</summary>
    public (bool Keep, string? Path, string? Refusal) Readiness(string? seed, int maxChars)
    {
        if (seed is null) return (true, null, null);
        if (string.Equals(seed, "tcp", StringComparison.OrdinalIgnoreCase)) return (false, null, null);
        if (!seed.StartsWith('/') || seed.Any(char.IsWhiteSpace) || seed.Any(char.IsControl))
            return (false, null, $"'{seed}' is not a readiness path: it starts with '/' and holds no blanks, or it is 'tcp' to probe the port");
        return seed.Length > maxChars
            ? (false, null, $"The readiness path is {seed.Length} characters; at most {maxChars} are allowed "
                + $"({PocsConfig.ReadinessPathMaxCharsKey}), because it is a probe path, not a payload")
            : (false, seed, null);
    }
}
