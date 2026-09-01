using AgentSmith.Application.Services.Handlers;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-08-31-7097: turns the verify stages a sandbox's contexts DECLARE into the
/// binaries the toolchain probe can look for — and into the stages it cannot read.
/// Pure transformation; the decision about a single command lives in
/// <see cref="BareCommandBinary"/>.
/// </summary>
public static class DeclaredStageBinaries
{
    public static DeclaredStageDerivation Derive(IReadOnlyList<ContextVerifyStages> contexts)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        var binaries = new List<DeclaredStageBinary>();
        var unprobed = new List<UnprobedStage>();
        foreach (var context in contexts)
        foreach (var stage in context.Stages)
        {
            if (BareCommandBinary.TryRead(stage.Command, out var binary))
                Add(binaries, new DeclaredStageBinary(binary, context.ContextName, stage.Label));
            else
                unprobed.Add(new UnprobedStage(context.ContextName, stage.Label, stage.Command));
        }
        return new DeclaredStageDerivation(binaries, unprobed);
    }

    // One sweep entry per binary: two stages naming `npm` ask the same question once,
    // and the FIRST stage that named it is the one a report can point at.
    private static void Add(List<DeclaredStageBinary> binaries, DeclaredStageBinary derived)
    {
        if (binaries.Any(b => string.Equals(b.Binary, derived.Binary, StringComparison.Ordinal)))
            return;
        binaries.Add(derived);
    }
}
