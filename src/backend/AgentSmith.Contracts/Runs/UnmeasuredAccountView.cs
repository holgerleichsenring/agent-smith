namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-10-12-6bfad: an account of a spec that could not be taken at all — the build was
/// red, the delivery diff would not run, the accounting call failed — with why.
/// <para>
/// It judged no criterion, so it is never shown or counted as one. A build that did not
/// run read as "0 of 1 proven" under a criterion titled after the sandbox key.
/// </para>
/// </summary>
/// <param name="Repositories">The repository key the account was taken over — one key, or
/// the comma-joined keys of every repository a spec-wide account spans.</param>
/// <param name="Reason">Why it could not be taken, verbatim.</param>
public sealed record UnmeasuredAccountView(string Repositories, string? Reason);
