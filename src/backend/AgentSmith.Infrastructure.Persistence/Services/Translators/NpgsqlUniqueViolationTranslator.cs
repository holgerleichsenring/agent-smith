using AgentSmith.Infrastructure.Persistence.Contracts;
using Npgsql;

namespace AgentSmith.Infrastructure.Persistence.Services.Translators;

/// <summary>
/// PostgreSQL: a unique-violation surfaces as PostgresException with SQLSTATE
/// 23505 (unique_violation).
/// </summary>
public sealed class NpgsqlUniqueViolationTranslator : IUniqueViolationTranslator
{
    private const string UniqueViolation = "23505";

    public bool IsUniqueViolation(Exception exception) =>
        Unwrap(exception) is PostgresException ex && ex.SqlState == UniqueViolation;

    private static Exception Unwrap(Exception exception) =>
        exception.InnerException ?? exception;
}
