namespace AgentSmith.Contracts.Events;

/// <summary>
/// Base type for all events published into the <c>system:events</c> stream.
/// Parallel to <see cref="RunEvent"/> but for pre-run activity that has no
/// runId — ticket polling cycles, webhook receipts, chat ingestion, trigger
/// evaluation, config-file reads, skill catalog activity. The dashboard
/// subscribes via <c>JobsHub.SubscribeSystem</c> and renders system-level
/// KPIs + activity lists from this stream.
///
/// <para><b>Source</b> is a free-form producer tag (e.g.
/// <c>tracker:jira/sample</c>, <c>webhook:github</c>, <c>chat:slack</c>,
/// <c>skill-catalog</c>, <c>config-loader</c>) so the dashboard can group
/// events by origin without parsing the type discriminator. Free-form by
/// design — see decisions: new producers add new strings without modifying
/// a shared enum.</para>
/// </summary>
public abstract record SystemEvent(string Source, SystemEventType Type, DateTimeOffset Timestamp);
