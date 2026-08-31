namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-08-30-f590: the tests that start a real external process run alone.
/// <para>
/// A two-core CI runner cannot do twenty npm installs, git invocations and sandbox
/// steps at once and still schedule anything else. Measured: unrelated tests taking
/// 66 ms locally took 35 to 39 seconds there, all finishing together — they were not
/// slow, they were waiting. Tests that assert something happens within a window then
/// report a defect that is not there.
/// </para>
/// <para>
/// Disabling parallelisation for this collection keeps the expensive work out of the
/// window in which everything else measures. It does not make the suite faster; it
/// makes it honest.
/// </para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ExternalProcessCollection
{
    public const string Name = "ExternalProcess";
}
