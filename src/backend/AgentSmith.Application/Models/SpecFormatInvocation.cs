namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-10-12-fbbdb: one formatter run the spec commit makes — the declaring context, the
/// command line with its files placeholder replaced, and how many files it names.
/// 2026-10-13-a347a: and the command as the context declared it, which is what the operator
/// corrects when it is wrong.
/// </summary>
public sealed record SpecFormatInvocation(string ContextName, string CommandLine, int FileCount, string DeclaredCommand);
