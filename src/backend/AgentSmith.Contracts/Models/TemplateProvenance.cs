namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-09-13-ed5a: one template the analysis that cut an epic had open, and the revision
/// it stood at while the cut was made.
/// <para>
/// It is a value on the OUTCOME rather than a live sandbox because the filer runs later and
/// elsewhere: the turn disposes its sandboxes before the outcome flow starts, and filing is
/// retried from the stored outcome in a later process. A revision read off a live scope
/// cannot reach the parent ticket; one carried here can.
/// </para>
/// </summary>
/// <param name="Address">The address the analysis read it by — <c>template:&lt;context&gt;</c>.</param>
/// <param name="Repo">The repository the template lives in.</param>
/// <param name="Revision">
/// The sha the scope landed on when the analysis actually opened it, the DECLARED revision
/// when it did not, and empty when the declaration named none.
/// </param>
/// <param name="Opened">
/// True when the analysis read the template. False says the template was open and unread,
/// which is a different claim from "read at this sha" — a parent body that blurred the two
/// would report a measurement nobody took.
/// </param>
public sealed record TemplateProvenance(string Address, string Repo, string Revision, bool Opened);
