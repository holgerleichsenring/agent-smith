using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.Sandbox;
using k8s;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Sandbox;

/// <summary>2026-11-25-9992b: the Kubernetes sandbox and workspace factories over one client, wired as the server wires them.</summary>
internal static class KubernetesFactoryDoubles
{
    public static KubernetesSandboxFactory Sandbox(
        IKubernetes client, IConnectionMultiplexer redis, KubernetesSandboxOptions options, SandboxBusCredentials buses) =>
        new(Pods(options), new KubernetesPodLauncher(client, options, NullLoggerFactory.Instance),
            Containers(client, redis, options), options, buses, new WorkspaceAgentProtocolGate());

    public static KubernetesWorkspaceFactory Workspace(
        IKubernetes client, IConnectionMultiplexer redis, KubernetesSandboxOptions options, SandboxBusCredentials buses) =>
        new(Pods(options), new KubernetesPodLauncher(client, options, NullLoggerFactory.Instance),
            Containers(client, redis, options), options, buses, new WorkspaceAgentProtocolGate(), NullLoggerFactory.Instance);

    private static PodSpecBuilder Pods(KubernetesSandboxOptions options) =>
        new(new SandboxPodLabels(new SandboxOwnerIdentity("store-test")), new PodSecurity(options));

    private static KubernetesContainerSandboxes Containers(
        IKubernetes client, IConnectionMultiplexer redis, KubernetesSandboxOptions options) =>
        new(client, redis, options, Options.Create(new SandboxGlobalConfig()), Mock.Of<IWireProtocolWatcher>(),
            NullLoggerFactory.Instance);
}
