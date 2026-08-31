namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-08-31-7097: what the declared verify stages of one sandbox could and could not
/// contribute to the toolchain probe — the binaries that can be looked for, and the
/// stages whose commands were not read.
/// </summary>
public sealed record DeclaredStageDerivation(
    IReadOnlyList<DeclaredStageBinary> Binaries, IReadOnlyList<UnprobedStage> Unprobed)
{
    public static DeclaredStageDerivation Empty { get; } = new([], []);

    public bool IsEmpty => Binaries.Count == 0 && Unprobed.Count == 0;
}
