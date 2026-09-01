namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-09-01-379a: the command a repository DECLARES as the question "does my target
/// environment answer?", read from the <c>probe:</c> block of its own context.yaml.
/// <para>
/// It sits beside <see cref="ContextYamlVerifyStage"/> because the repository is already
/// the authority on what proves a change in it, and a command that proves a target
/// answers is target-specific in exactly the same way. Deriving one per ecosystem would
/// be the guessing this codebase has removed twice.
/// </para>
/// <para>
/// A command that resolves authentication before it does anything else — a warehouse
/// CLI's validate, a cluster CLI's whoami — is not broken when it reds on a clean tree.
/// It is uncredentialed. With the credential it is the cheapest true statement about the
/// environment a run can buy, and buying it before the master spends a token is the point.
/// </para>
/// </summary>
/// <param name="Target">What answers, in the operator's own words ("the warehouse dev
/// workspace"). It is what the failure names, because "exit 1" without the target is a
/// fact about a process rather than about the estate.</param>
/// <param name="Command">The command line, run through /bin/sh -c at the declaring
/// context's workdir — the same shell the verify gate uses, so an <c>&amp;&amp;</c> or a
/// <c>$VAR</c> means here what it means there.</param>
public sealed record ContextYamlTargetProbe(string Target, string Command);
