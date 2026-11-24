namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933db: the states of a PoC revision — admitted by the import, then validated or refused by the
/// poc-deploy run's intake, never evaluated twice; 2026-11-23-933dc: blocked by the code scan, or overruled; 2026-11-23-933dd: built, or blocked by the image scan.</summary>
public static class PocRevisionStates
{
    public const string Admitted = "admitted";
    public const string Validated = "validated";
    public const string Refused = "refused";
    public const string Blocked = "blocked";
    public const string Overruled = "overruled";

    /// <summary>2026-11-23-933dd: its image was built, scanned and pushed; the revision carries the digest.</summary>
    public const string Built = "built";

    /// <summary>A revision the intake already evaluated — it is never evaluated again.</summary>
    public static bool IsEvaluated(string state) => state is Validated or Refused or Blocked or Overruled or Built;
}
