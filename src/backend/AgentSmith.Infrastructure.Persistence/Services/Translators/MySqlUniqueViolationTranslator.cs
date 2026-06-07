using AgentSmith.Infrastructure.Persistence.Contracts;
using MySqlConnector;

namespace AgentSmith.Infrastructure.Persistence.Services.Translators;

/// <summary>
/// MySQL: a duplicate-key surfaces as MySqlException with error number 1062
/// (ER_DUP_ENTRY). Pomelo runs on MySqlConnector, whose exception type carries
/// the numeric error code.
/// </summary>
public sealed class MySqlUniqueViolationTranslator : IUniqueViolationTranslator
{
    private const int DuplicateEntry = 1062;

    public bool IsUniqueViolation(Exception exception) =>
        Unwrap(exception) is MySqlException ex && ex.Number == DuplicateEntry;

    private static Exception Unwrap(Exception exception) =>
        exception.InnerException ?? exception;
}
