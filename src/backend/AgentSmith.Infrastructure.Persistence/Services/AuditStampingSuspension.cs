namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-08-28-2af6: the scope returned by <c>AgentSmithDbContext.SuspendAuditStamping</c>.
/// While it lives, a save leaves CreatedAt and UpdatedAt exactly as the caller set them;
/// disposing it puts the stamping back, whether the caller left normally or threw.
/// </summary>
internal sealed class AuditStampingSuspension : IDisposable
{
    private readonly AgentSmithDbContext _context;

    public AuditStampingSuspension(AgentSmithDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _context.SetAuditSuspended(true);
    }

    public void Dispose() => _context.SetAuditSuspended(false);
}
