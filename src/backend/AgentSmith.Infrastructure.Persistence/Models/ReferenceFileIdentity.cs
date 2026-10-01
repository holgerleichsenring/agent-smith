namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>
/// 2026-10-01-283da: where the reference-file identity starts. Every id below it was born in the
/// legacy image table and keeps its number when copied; every id at or above it was born here.
/// A billion is above any image count this estate will reach and below the 32-bit ceiling the
/// shared migration set's INTEGER literal means on MySQL and Postgres.
/// </summary>
public static class ReferenceFileIdentity
{
    public const long Seed = 1_000_000_000;
}
