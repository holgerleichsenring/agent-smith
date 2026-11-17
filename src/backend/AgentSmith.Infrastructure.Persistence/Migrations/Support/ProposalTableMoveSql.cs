namespace AgentSmith.Infrastructure.Persistence.Migrations.Support;

/// <summary>
/// 2026-11-17-a0b6d: the data copy of MoveProposalsToOneTable, spelled once for every provider's migration set — each set
/// passes its own quoting and string concatenation. Up moves each session's proposals into rows; Down puts them back.
/// <para>
/// UP, PER SESSION AND IN THIS ORDER: the confirmed JSON (filing, under ConfirmedProposalId — or, from before 205fe, the
/// filing ProposalId or 'migrated-confirmed-{SessionId}'), the awaiting JSON (awaiting, under ProposalId), the reviewing
/// JSON (reviewing, no id), and a LatestProposalJson a pre-ccd9 build left with no id — reviewing when it carries
/// "underReview":true, as the read-time backfill decided, else awaiting under 'migrated-{SessionId}'. Each lands on the
/// newest stateless turn row of the session whose JSON is equal, or on a new row keyed 'proposal:{id}' (a draft
/// 'proposal:review:{SessionId}').
/// </para>
/// <para>
/// Every UPDATE selects the rows it changes through a derived table, because MySQL — which runs the SQLite set — refuses a
/// subquery on the table being updated.
/// </para>
/// </summary>
public sealed class ProposalTableMoveSql(string open, string close, Func<string, string, string> concat)
{
    private const string Proposals = "SpecDialogProposal";
    private const string Sessions = "SpecDialogSessions";
    private const string Unbound = Entities.SpecDialogProposal.UnboundPrefix;
    private const string UnderReview = "%\"underReview\":true%";

    /// <summary>SQLite and PostgreSQL: double quotes and ||.</summary>
    public static ProposalTableMoveSql Ansi { get; } = new("\"", "\"", (a, b) => $"({a} || {b})");

    /// <summary>SQL Server: brackets and +.</summary>
    public static ProposalTableMoveSql SqlServer { get; } = new("[", "]", (a, b) => $"({a} + {b})");

    /// <summary>MySQL: backticks and CONCAT.</summary>
    public static ProposalTableMoveSql MySql { get; } = new("`", "`", (a, b) => $"CONCAT({a}, {b})");

    /// <summary>The SQLite set's dialect: MySQL's when it runs there, the ANSI one otherwise.</summary>
    public static ProposalTableMoveSql ForSharedSet(string? activeProvider) =>
        activeProvider == "Pomelo.EntityFrameworkCore.MySql" ? MySql : Ansi;

    private string Q(string name) => $"{open}{name}{close}";

    private string S(string column) => $"s.{Q(column)}";

    /// <summary>The statements that copy every session's proposals into rows; run after the columns are added and before
    /// the session columns are dropped.</summary>
    public IEnumerable<string> Up()
    {
        var confirmedId = $"COALESCE({S("ConfirmedProposalId")}, CASE WHEN {S("ProposalState")} = 'filing' THEN {S("ProposalId")} END, "
            + $"{concat("'migrated-confirmed-'", S("SessionId"))})";
        var latestOnly = $"{S("ProposalId")} IS NULL AND {S("AwaitingProposalJson")} IS NULL AND {S("ReviewingProposalJson")} IS NULL "
            + $"AND {S("LatestProposalJson")} IS NOT NULL";
        var migratedId = concat("'migrated-'", S("SessionId"));
        var draftKey = concat($"'{Unbound}review:'", S("SessionId"));
        return
        [
            .. Stage($"{S("ConfirmedOutcomeJson")} IS NOT NULL", "ConfirmedOutcomeJson", confirmedId, "filing", null),
            .. Stage($"{S("ProposalState")} = 'awaiting' AND {S("ProposalId")} IS NOT NULL AND {S("AwaitingProposalJson")} IS NOT NULL",
                "AwaitingProposalJson", S("ProposalId"), "awaiting", null),
            .. Stage($"{S("ReviewingProposalJson")} IS NOT NULL", "ReviewingProposalJson", null, "reviewing", draftKey),
            .. Stage($"{latestOnly} AND {S("LatestProposalJson")} LIKE '{UnderReview}'", "LatestProposalJson", null, "reviewing", draftKey),
            .. Stage($"{latestOnly} AND {S("LatestProposalJson")} NOT LIKE '{UnderReview}'", "LatestProposalJson", migratedId, "awaiting", null),
        ];
    }

    /// <summary>The statements that put the proposals back on the session columns — the newest filing, the awaiting and
    /// the reviewing row — and drop the rows no run wrote; run after the columns are added back.</summary>
    public IEnumerable<string> Down()
    {
        string Newest(string state, string column) =>
            $"(SELECT p.{Q(column)} FROM {Q(Proposals)} p WHERE p.{Q("Id")} = (SELECT MAX(q.{Q("Id")}) FROM {Q(Proposals)} q "
            + $"WHERE q.{Q("SessionId")} = {Q(Sessions)}.{Q("SessionId")} AND q.{Q("State")} = '{state}'))";
        string Has(string state) =>
            $"EXISTS (SELECT 1 FROM {Q(Proposals)} p WHERE p.{Q("SessionId")} = {Q(Sessions)}.{Q("SessionId")} AND p.{Q("State")} = '{state}')";
        return
        [
            $"UPDATE {Q(Sessions)} SET {Q("ConfirmedOutcomeJson")} = {Newest("filing", "ProposalJson")}, "
                + $"{Q("ConfirmedProposalId")} = {Newest("filing", "ProposalId")}, {Q("ProposalId")} = {Newest("filing", "ProposalId")}, "
                + $"{Q("ProposalState")} = 'filing' WHERE {Has("filing")};",
            $"UPDATE {Q(Sessions)} SET {Q("AwaitingProposalJson")} = {Newest("awaiting", "ProposalJson")}, "
                + $"{Q("LatestProposalJson")} = {Newest("awaiting", "ProposalJson")}, {Q("ProposalId")} = {Newest("awaiting", "ProposalId")}, "
                + $"{Q("ProposalState")} = 'awaiting' WHERE {Has("awaiting")};",
            $"UPDATE {Q(Sessions)} SET {Q("ReviewingProposalJson")} = {Newest("reviewing", "ProposalJson")} WHERE {Has("reviewing")};",
            $"DELETE FROM {Q(Proposals)} WHERE {Q("RunId")} LIKE '{Unbound}%';",
        ];
    }

    /// <summary>One proposal kind: onto the session's newest stateless turn row with equal JSON, else onto a new row.</summary>
    private IEnumerable<string> Stage(string where, string json, string? id, string state, string? key)
    {
        var idValue = id ?? "NULL";
        var matched = $"SELECT MAX(p.{Q("Id")}) AS {Q("Id")} FROM {Q(Proposals)} p JOIN {Q(Sessions)} s ON s.{Q("SessionId")} = p.{Q("SessionId")} "
            + $"WHERE {where} AND p.{Q("State")} IS NULL AND p.{Q("ProposalJson")} = {S(json)} GROUP BY p.{Q("SessionId")}";
        yield return $"UPDATE {Q(Proposals)} SET {Q("ProposalId")} = (SELECT {idValue} FROM {Q(Sessions)} s "
            + $"WHERE s.{Q("SessionId")} = {Q(Proposals)}.{Q("SessionId")}), {Q("State")} = '{state}' "
            + $"WHERE {Q("Id")} IN (SELECT m.{Q("Id")} FROM ({matched}) m);";
        yield return $"INSERT INTO {Q(Proposals)} ({Q("SessionId")}, {Q("RunId")}, {Q("ProposalJson")}, {Q("ProposalId")}, {Q("State")}, "
            + $"{Q("CreatedAt")}, {Q("UpdatedAt")}) SELECT s.{Q("SessionId")}, {key ?? concat($"'{Unbound}'", idValue)}, {S(json)}, {idValue}, "
            + $"'{state}', {S("LastActivityAt")}, {S("LastActivityAt")} FROM {Q(Sessions)} s WHERE {where} AND NOT EXISTS "
            + $"(SELECT 1 FROM {Q(Proposals)} p WHERE p.{Q("SessionId")} = s.{Q("SessionId")} AND p.{Q("State")} = '{state}');";
    }
}
