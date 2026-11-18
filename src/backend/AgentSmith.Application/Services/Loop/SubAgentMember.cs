using AgentSmith.Application.Services.Handlers;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// 2026-11-18-c8e2a: one child of a master — its id and spec, the inbox its lead writes to, the conversation it has
/// had so far (so a continuation keeps its context), the request its passes are built from, and whether a pass of it
/// is running now.
/// </summary>
public sealed class SubAgentMember(string id, SubAgentSpec spec)
{
    /// <summary>The heading a master's word reaches its child under.</summary>
    public const string LeadHeading = "Your master wrote while you were working";

    private readonly Lock _gate = new();
    private bool _running;

    public string Id { get; } = id;
    public SubAgentSpec Spec { get; } = spec;
    public string Name => Spec.Name;
    public SubAgentInbox Inbox { get; } = new();
    public MasterConversation Conversation { get; } = new();

    /// <summary>The request of its first pass; a continuation is this request over the kept conversation.</summary>
    public AgenticLoopRequest? Request { get; set; }

    /// <summary>The pass running now, or the last one.</summary>
    public Task Pass { get; set; } = Task.CompletedTask;

    public bool IsRunning
    {
        get { lock (_gate) return _running; }
    }

    public static SubAgentMember For(SubAgentSpec spec) => new($"sa-{Guid.NewGuid():N}"[..8], spec);

    /// <summary>Marks a pass begun; false when one already runs.</summary>
    public bool Begin()
    {
        lock (_gate)
        {
            if (_running) return false;
            _running = true;
            return true;
        }
    }

    public void End()
    {
        lock (_gate) _running = false;
    }

    /// <summary>Posts a lead's word while a pass runs; false when none runs, so the caller continues it instead.</summary>
    public bool TryPost(string text)
    {
        lock (_gate)
        {
            if (!_running) return false;
            Inbox.Post(text, LeadHeading);
            return true;
        }
    }
}
