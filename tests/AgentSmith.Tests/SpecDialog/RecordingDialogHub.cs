using System.Collections.Concurrent;
using AgentSmith.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-15-9033: a JobsHub context that records what was pushed and where. Every
/// addressing mode except Group throws, because the dashboard channel delivers a design
/// conversation to the one session that holds it — a broadcast here would be the bug.
/// </summary>
internal sealed class RecordingDialogHub : IHubContext<JobsHub>, IHubClients, IGroupManager
{
    private const string GroupsOnly =
        "The dashboard spec-dialog channel addresses one session group and nothing else.";

    /// <summary>
    /// Concurrent because a turn is dispatched fire-and-forget: a test reads this while
    /// the turn that fills it is still running.
    /// </summary>
    public ConcurrentQueue<HubPush> Pushes { get; } = new();
    public List<string> Joined { get; } = [];

    IHubClients IHubContext<JobsHub>.Clients => this;
    IGroupManager IHubContext<JobsHub>.Groups => this;

    public IClientProxy Group(string groupName) => new GroupProxy(this, groupName);

    public Task AddToGroupAsync(
        string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        Joined.Add(groupName);
        return Task.CompletedTask;
    }

    public Task RemoveFromGroupAsync(
        string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        Joined.Remove(groupName);
        return Task.CompletedTask;
    }

    public IClientProxy All => throw new NotSupportedException(GroupsOnly);
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) =>
        throw new NotSupportedException(GroupsOnly);
    public IClientProxy Client(string connectionId) => throw new NotSupportedException(GroupsOnly);
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) =>
        throw new NotSupportedException(GroupsOnly);
    public IClientProxy Groups(IReadOnlyList<string> groupNames) =>
        throw new NotSupportedException(GroupsOnly);
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) =>
        throw new NotSupportedException(GroupsOnly);
    public IClientProxy User(string userId) => throw new NotSupportedException(GroupsOnly);
    public IClientProxy Users(IReadOnlyList<string> userIds) =>
        throw new NotSupportedException(GroupsOnly);

    internal sealed record HubPush(string Group, string Method, object?[] Args);

    private sealed class GroupProxy(RecordingDialogHub hub, string group) : IClientProxy
    {
        public Task SendCoreAsync(
            string method, object?[] args, CancellationToken cancellationToken = default)
        {
            hub.Pushes.Enqueue(new HubPush(group, method, args));
            return Task.CompletedTask;
        }
    }
}
