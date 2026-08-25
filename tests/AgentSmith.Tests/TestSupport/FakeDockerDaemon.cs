using Docker.DotNet;
using Docker.DotNet.Models;
using Moq;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// A Docker daemon that APPLIES the label filters it is handed, instead of a mock that
/// returns a canned list whatever is asked. p0465 turns on a query term, so a rig that
/// ignores filters would pass with the term removed — the bug it exists to catch.
/// Supports the two label filter forms Docker does: <c>key</c> (present) and
/// <c>key=value</c> (equals).
/// </summary>
internal sealed class FakeDockerDaemon
{
    private readonly List<ContainerListResponse> _containers;

    internal FakeDockerDaemon(params ContainerListResponse[] containers)
    {
        _containers = [.. containers];
        Client = BuildClient();
    }

    internal IDockerClient Client { get; }

    /// <summary>Container ids returned by every list call so far.</summary>
    internal List<string> Listed { get; } = [];

    /// <summary>Container ids the caller force-removed.</summary>
    internal List<string> Removed { get; } = [];

    private IDockerClient BuildClient()
    {
        var containers = new Mock<IContainerOperations>();
        containers
            .Setup(c => c.ListContainersAsync(It.IsAny<ContainersListParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContainersListParameters p, CancellationToken _) => List(p));
        containers
            .Setup(c => c.RemoveContainerAsync(
                It.IsAny<string>(), It.IsAny<ContainerRemoveParameters>(), It.IsAny<CancellationToken>()))
            .Returns((string id, ContainerRemoveParameters _, CancellationToken _) =>
            {
                Removed.Add(id);
                _containers.RemoveAll(c => c.ID == id);
                return Task.CompletedTask;
            });

        var volumes = new Mock<IVolumeOperations>();
        volumes
            .Setup(v => v.RemoveAsync(It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var client = new Mock<IDockerClient>();
        client.SetupGet(d => d.Containers).Returns(containers.Object);
        client.SetupGet(d => d.Volumes).Returns(volumes.Object);
        return client.Object;
    }

    private IList<ContainerListResponse> List(ContainersListParameters parameters)
    {
        var matched = _containers.Where(c => Matches(c, parameters)).ToList();
        Listed.AddRange(matched.Select(c => c.ID));
        return matched;
    }

    private static bool Matches(ContainerListResponse container, ContainersListParameters parameters)
    {
        if (parameters.Filters is null || !parameters.Filters.TryGetValue("label", out var terms)) return true;
        return terms.Where(t => t.Value).Select(t => t.Key).All(term => HasLabel(container, term));
    }

    private static bool HasLabel(ContainerListResponse container, string term)
    {
        var separator = term.IndexOf('=');
        if (separator < 0) return container.Labels?.ContainsKey(term) == true;
        var key = term[..separator];
        return container.Labels is { } labels
               && labels.TryGetValue(key, out var value)
               && value == term[(separator + 1)..];
    }
}
