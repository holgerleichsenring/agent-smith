namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-c6ec: a client file the reading could not decide — reachable and read, but
/// what it calls could not be established (a call built from a variable, a generated
/// client, a file too large to hold).
/// <para>
/// It is named rather than dropped because a missed call site MANUFACTURES a finding: an
/// operation a client really exercises looks unexercised. Every one of these degrades the
/// claim the difference makes.
/// </para>
/// </summary>
public sealed record UndecidedClientFile(string File, string Why);
