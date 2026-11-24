using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace AgentSmith.Tests.Pocs;

/// <summary>
/// 2026-11-23-933db: the PoCs server of <see cref="PocsAuthorityFixture"/> with the queue recorded instead of pushed and
/// the configuration store held in memory, so a first import provisions its poc- project without a catalog to check it
/// against. <see cref="Queued"/> holds every dispatched request, <see cref="Projects"/> every saved project.
/// </summary>
public class PocImportFixture : PocsAuthorityFixture
{
    /// <summary>The per-person limit while a case does not test it — the cases share one store and one owner.</summary>
    internal const int RoomyLimit = 100;

    public PocImportFixture() => Pocs.Targets[0].PerPersonLimit = RoomyLimit;

    internal List<PipelineRequest> Queued { get; } = [];

    internal List<ProjectEntity> Projects { get; } = [];

    protected override Action<IServiceCollection>? Services => services =>
    {
        base.Services!(services);
        var queue = new Mock<IRedisJobQueue>();
        queue.Setup(q => q.EnqueueAsync(It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PipelineRequest, CancellationToken>((request, _) => { lock (Queued) Queued.Add(request); })
            .Returns(Task.CompletedTask);
        services.RemoveAll<IRedisJobQueue>();
        services.AddSingleton(queue.Object);
        var store = new Mock<IConfigStore>();
        store.Setup(s => s.GetProjects()).Returns(() => { lock (Projects) return [.. Projects]; });
        store.Setup(s => s.UpsertProject(It.IsAny<ProjectEntity>(), It.IsAny<ChangeAttribution>()))
            .Callback<ProjectEntity, ChangeAttribution>((project, _) =>
            {
                lock (Projects) { Projects.RemoveAll(p => p.Id == project.Id); Projects.Add(project); }
            });
        services.RemoveAll<IConfigStore>();
        services.AddSingleton(store.Object);
    };
}
