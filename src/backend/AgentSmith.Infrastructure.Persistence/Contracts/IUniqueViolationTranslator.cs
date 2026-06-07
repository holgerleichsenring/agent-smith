namespace AgentSmith.Infrastructure.Persistence.Contracts;

/// <summary>
/// Maps a provider-native unique-constraint violation to a boolean the claim
/// service turns into AlreadyClaimed. Provider-specific by design: the SQLSTATE/
/// error number differs (Postgres 23505 / MySQL 1062 / SQLite 19), so a blanket
/// DbUpdateException catch would swallow unrelated write failures.
/// </summary>
public interface IUniqueViolationTranslator
{
    bool IsUniqueViolation(Exception exception);
}
