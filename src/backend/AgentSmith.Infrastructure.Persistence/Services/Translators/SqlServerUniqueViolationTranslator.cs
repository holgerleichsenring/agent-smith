using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.Data.SqlClient;

namespace AgentSmith.Infrastructure.Persistence.Services.Translators;

/// <summary>
/// SQL Server: a unique-violation surfaces as SqlException with error number
/// 2627 (unique constraint) or 2601 (unique index).
/// </summary>
public sealed class SqlServerUniqueViolationTranslator : IUniqueViolationTranslator
{
    private const int UniqueConstraintViolation = 2627;
    private const int UniqueIndexViolation = 2601;

    public bool IsUniqueViolation(Exception exception) =>
        Unwrap(exception) is SqlException ex
        && ex.Number is UniqueConstraintViolation or UniqueIndexViolation;

    private static Exception Unwrap(Exception exception) =>
        exception.InnerException ?? exception;
}
