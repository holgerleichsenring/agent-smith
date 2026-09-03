namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-08-31-26d4: one command a repository DECLARES as proof that a change in it
/// holds, read from the <c>verify:</c> block of its own context.yaml.
/// <para>
/// Outside .NET the only source of a build or test command was what the analyzer
/// emitted for that run, so the gate differed between runs — the same party marking
/// its own work with a ruler it had just drawn. A declaration is authored once and
/// executed unchanged, which is what makes it a second opinion.
/// </para>
/// <para>
/// Three fields, and the fourth would be the one that invents a taxonomy. What a
/// stage IS follows from its command; a <c>kind</c> or a <c>category</c> would have to
/// be a closed list, and a closed list of somebody else's estate vocabulary is the
/// idea this repository has now lost three times.
/// </para>
/// </summary>
/// <param name="Label">What this stage is called in the run's outcome ("build", "lint").</param>
/// <param name="Command">The command line, run through /bin/sh -c at the REPOSITORY ROOT
/// (2026-09-03-7bac). A command needing another directory carries its own cd.</param>
/// <param name="WhenPresent">Optional path, relative to the repository root, the command
/// needs in order to mean anything. Absent path =&gt; the stage is skipped and reported, never
/// failed: verification stops at the first non-zero exit, so a red it was never measured
/// for would hide every real gate behind it.</param>
public sealed record ContextYamlVerifyStage(
    string Label, string Command, string? WhenPresent = null);
