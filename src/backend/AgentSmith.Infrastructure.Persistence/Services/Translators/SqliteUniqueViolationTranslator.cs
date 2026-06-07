using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.Data.Sqlite;

namespace AgentSmith.Infrastructure.Persistence.Services.Translators;

/// <summary>
/// SQLite: a unique-index violation surfaces as SqliteException with
/// SqliteErrorCode 19 (SQLITE_CONSTRAINT). The extended code 2067
/// (SQLITE_CONSTRAINT_UNIQUE) is the precise unique case; we accept the
/// primary code 19 too since the only constraint on ActiveRun is the unique one.
/// </summary>
public sealed class SqliteUniqueViolationTranslator : IUniqueViolationTranslator
{
    private const int SqliteConstraint = 19;
    private const int SqliteConstraintUnique = 2067;

    public bool IsUniqueViolation(Exception exception) =>
        Unwrap(exception) is SqliteException ex
        && (ex.SqliteErrorCode == SqliteConstraint
            || ex.SqliteExtendedErrorCode == SqliteConstraintUnique);

    private static Exception Unwrap(Exception exception) =>
        exception.InnerException ?? exception;
}
