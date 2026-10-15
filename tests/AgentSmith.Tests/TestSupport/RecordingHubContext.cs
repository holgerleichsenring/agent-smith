using AgentSmith.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-10-15-4a4dc: a hub context that records every send — the groups it was addressed to, the
/// message name and the arguments — so a case can say who a nudge reached.
/// </summary>
internal sealed class RecordingHubContext
{
    private readonly List<Sent> _sent = [];

    public RecordingHubContext()
    {
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns<string>(group => Proxy([group]));
        clients.Setup(c => c.Groups(It.IsAny<IReadOnlyList<string>>())).Returns<IReadOnlyList<string>>(Proxy);
        var hub = new Mock<IHubContext<JobsHub>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);
        Hub = hub.Object;
    }

    public IHubContext<JobsHub> Hub { get; }

    public IReadOnlyList<Sent> Messages => _sent;

    private IClientProxy Proxy(IReadOnlyList<string> groups)
    {
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((method, args, _) => _sent.Add(new([.. groups], method, args)))
            .Returns(Task.CompletedTask);
        return proxy.Object;
    }

    internal sealed record Sent(IReadOnlyList<string> Groups, string Method, object?[] Arguments);
}
