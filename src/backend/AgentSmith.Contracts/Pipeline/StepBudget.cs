using AgentSmith.Contracts.Commands;

namespace AgentSmith.Contracts.Pipeline;

/// <summary>
/// 2026-09-17-0e79e: how many command executions one run segment may spend before the
/// executor calls it a loop. The number used to be a constant sized for a pipeline whose
/// length was known at configuration time; a coding run's length is not — it depends on how
/// many specs the ticket was cut into, and each spec splices its own block, its repair
/// pass, its premise check and its review.
/// <para>
/// So the guard that actually stops an insertion loop is <see cref="ForSpecs"/>: base plus
/// per-spec times the specs the run will splice. <see cref="AbsoluteCeiling"/> never binds
/// while the spec cap is what it is — it exists so a later cap raise cannot silently unbound
/// the guard. A pipeline that splices nothing publishes nothing and keeps
/// <see cref="Default"/>, which is the number every preset has always had.
/// </para>
/// <para>
/// A plain value on purpose: it travels through PipelineContextSerializer into a checkpoint
/// and back out on resume, where no sequence runs again to re-publish it.
/// </para>
/// </summary>
/// <param name="Limit">Command executions this segment may spend.</param>
/// <param name="Origin">Where the number came from, for the log and the exhaustion message.</param>
public sealed record StepBudget(int Limit, string Origin)
{
    /// <summary>What a pipeline that splices no blocks has always been allowed.</summary>
    public const int Default = 100;

    /// <summary>
    /// The run's own steps — everything the preset lists around the spec blocks (22 literal
    /// commands in the code preset today), with room for the ones a run inserts for itself.
    /// </summary>
    public const int BaseAllowance = 40;

    /// <summary>
    /// One spec: its six-command block plus the repair pass, the premise check, the review
    /// and the fix pass that can follow it — about seventeen at worst, rounded up.
    /// </summary>
    public const int PerSpecAllowance = 20;

    /// <summary>
    /// The number no spec count can exceed. It is not the working guard — at the current
    /// spec cap the computed budget is well below it — it is the stop that keeps the guard
    /// bounded if the cap is ever raised.
    /// </summary>
    public const int AbsoluteCeiling = 400;

    /// <summary>The budget of a run that splices nothing.</summary>
    public static StepBudget TheDefault => new(Default, "no spec sequence");

    /// <summary>The budget for a run that will splice <paramref name="specs"/> spec blocks.</summary>
    public static StepBudget ForSpecs(int specs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(specs);
        var computed = BaseAllowance + (PerSpecAllowance * specs);
        return new StepBudget(
            Math.Min(computed, AbsoluteCeiling),
            computed > AbsoluteCeiling
                ? $"{specs} spec(s) x {PerSpecAllowance} + {BaseAllowance} base, clamped at {AbsoluteCeiling}"
                : $"{specs} spec(s) x {PerSpecAllowance} + {BaseAllowance} base");
    }

    /// <summary>
    /// The budget published for this run, or <see cref="TheDefault"/> when nothing published
    /// one. Read on every loop pass: the sequence publishes mid-run, and a resume reads it
    /// back out of the rehydrated context.
    /// </summary>
    public static StepBudget From(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.TryGet<StepBudget>(ContextKeys.StepBudget, out var published)
               && published is not null
            ? published
            : TheDefault;
    }

    /// <summary>What the log line and the exhaustion message both say.</summary>
    public override string ToString() => $"step budget {Limit} ({Origin})";
}
