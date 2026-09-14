using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Services.Preflight;

/// <summary>
/// 2026-09-14-3f5b: whether sign-in can work at this installation, answered before an
/// operator finds out by switching enforcement on.
/// <para>
/// Two questions nobody thinks to ask until it is too late. Has a token actually been
/// accepted here — an authority that is configured, reachable and validating is still not
/// one anybody has got through. And is there an administrator this server can SEE, because
/// enforcement with nobody able to reach the access surface is a lockout the next restart
/// makes permanent.
/// </para>
/// <para>
/// SERVER-ONLY, and therefore not part of `agentsmith doctor`. Two of its facts do not exist
/// in the CLI's graph — <see cref="AdminGrant"/> and <see cref="RoleMappingSource"/> are
/// internal to this assembly and <see cref="IObservedCallerStore"/> resolves to nothing
/// there — so it is registered beside <see cref="ServerMemoryFloorCheck"/> and what it
/// reports reaches the startup report and the health surface.
/// </para>
/// </summary>
internal sealed class SignInCheck(
    TokenAuthorityConfig auth,
    RoleMappingSource mapping,
    AdminGrant grant,
    IObservedCallerStore observed) : IPreflightCheck
{
    public string Name => "sign-in";

    public string Category => "auth";

    public async Task<PreflightCheckResult> RunAsync(CancellationToken cancellationToken)
    {
        // An installation with no authority refuses nobody and is where every one of them
        // starts. There is nothing here to be wrong yet.
        if (!auth.IsUsable)
            return PreflightCheckResult.Skip(
                "no authority is configured, so no token is validated and no caller is refused");

        var inForce = mapping.Current().Mapping;
        var facts = await SignInFacts.ReadAsync(inForce, grant, observed, cancellationToken);
        var found = $"{Accepted(facts)}; {Administrators(facts)}";

        if (!auth.Enforce)
            return PreflightCheckResult.Pass(
                $"authority configured, enforcement OFF — nothing is refused. {found}");

        var problems = Problems(facts);
        return problems.Count == 0
            ? PreflightCheckResult.Pass($"authority configured, enforcement ON. {found}")
            : PreflightCheckResult.Fail(
                string.Join(" ", problems.Select(p => p.Finding)),
                string.Join(" ", problems.Select(p => p.Hint)));
    }

    private static List<(string Finding, string Hint)> Problems(SignInFacts facts)
    {
        var problems = new List<(string, string)>();

        // Read AND empty. A store that could not be read says nothing about who has signed
        // in, and reporting it as "nobody" sends an operator to fix a working sign-in.
        if (facts.WasRead && !facts.AnyAccepted)
            problems.Add((
                $"Enforcement is ON and no caller has been accepted in the last {facts.RetentionDays} "
                + "days, so every route is refusing and nothing here has been proven to get through.",
                "Sign in once before relying on this: /api/auth/requirements names which check "
                + "refuses a token, beside what the token carried."));

        if (facts.AdminRoutes.Count == 0)
            problems.Add((
                "Enforcement is ON and no administrator can be seen from here.",
                $"Grant one of the four routes: a person grant, a group mapped onto "
                + $"'{BuiltInRoles.Admin}', a directory role claim carrying it, or "
                + $"{AdminGrant.EnvVar} naming a subject or a group. The environment grant is the "
                + "one that works when the surface that writes the others cannot be reached."));

        return problems;
    }

    private static string Accepted(SignInFacts facts) => facts switch
    {
        { WasRead: false } =>
            "the observed-caller store could not be read, so whether anybody has signed in is unknown",
        { AnyAccepted: false } => $"no caller accepted in the last {facts.RetentionDays} days",
        _ => $"{facts.Seen!.Count} caller(s) accepted in the last {facts.RetentionDays} days, "
            + $"most recently {facts.LastAccepted:yyyy-MM-dd}",
    };

    private static string Administrators(SignInFacts facts) =>
        facts.AdminRoutes.Count == 0
            ? "no administrator visible from here"
            : $"administrator reachable through {string.Join(", ", facts.AdminRoutes)}";
}
