using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-08-28-b883: a money column keeps the fraction that is written into it. A single
/// model call costs a fraction of a cent — an observed run spent between 0.00032 and 0.034
/// per call — and SQL Server's default decimal mapping is decimal(18,2), which rounds every
/// one of those to 0.00 on the way in.
/// <para>
/// Only SQL Server is told a precision. SQLite stores decimal as text, PostgreSQL as an
/// unconstrained numeric and MySQL at a scale of 30 — all three already return what they
/// were given, and pinning them to this scale would narrow their range for nothing. The
/// provider is asked by name so the shared model snapshot stays exactly as it is.
/// </para>
/// </summary>
public sealed class MoneyPrecisionConfiguration(string? providerName)
{
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

    /// <summary>
    /// Eighteen digits total, ten of them fractional: a resolution of 0.0000000001 USD —
    /// three orders below the cheapest call any current price list can produce (a single
    /// cache-read token at 0.025 per million is 0.000000025) — over an integral range of
    /// eight digits, which is four orders above the largest run total or cap ever recorded.
    /// </summary>
    private const int MoneyPrecision = 18;

    private const int MoneyScale = 10;

    public void Apply(ModelBuilder modelBuilder)
    {
        if (providerName != SqlServerProvider) return;
        modelBuilder.Entity<Run>().Property(r => r.CostTotalUsd).HasPrecision(MoneyPrecision, MoneyScale);
        modelBuilder.Entity<Run>().Property(r => r.BudgetCapUsd).HasPrecision(MoneyPrecision, MoneyScale);
        modelBuilder.Entity<RunLlmCall>().Property(c => c.CostUsd).HasPrecision(MoneyPrecision, MoneyScale);
    }
}
