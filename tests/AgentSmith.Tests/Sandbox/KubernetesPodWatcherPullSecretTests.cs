using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;
using k8s.Models;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-08-31-46d7: kubelet reports the registry's rejection but never reports a MISSING
/// pull secret — a name matching no Secret is ignored and the pull simply goes out
/// unauthenticated. So the watcher's fatal-state message carries what the sandbox spec
/// DECLARED, never anything looked up in the cluster. The fatal decision is a pure
/// function over one container status, so it is proven here without a k8s client.
/// </summary>
public sealed class KubernetesPodWatcherPullSecretTests
{
    private static V1ContainerStatus Waiting(string reason, string message) => new()
    {
        Name = "toolchain",
        State = new V1ContainerState
        {
            Waiting = new V1ContainerStateWaiting { Reason = reason, Message = message }
        }
    };

    private static Action Watch(V1ContainerStatus status, params string[] pullSecrets) =>
        () => KubernetesPodWatcher.ThrowIfWaitingFatal("pod-1", status, "container", pullSecrets);

    [Fact]
    public void PodWatcher_AnImagePullFailure_NamesTheConfiguredPullSecrets()
    {
        var status = Waiting("ImagePullBackOff", "unauthorized: authentication required");

        var message = Watch(status, "acr-pull", "ghcr-pull")
            .Should().Throw<InvalidOperationException>().Which.Message;

        message.Should().Contain("ImagePullBackOff")
            .And.Contain("unauthorized: authentication required")
            .And.Contain("acr-pull")
            .And.Contain("ghcr-pull");
    }

    [Fact]
    public void ThrowIfWaitingFatal_ImagePullWithNoPullSecrets_SaysNoneWasConfigured()
    {
        var status = Waiting("ErrImagePull", "denied");

        Watch(status).Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("No image pull secret was configured")
            .And.Contain("sandbox.image_pull_secrets");
    }

    [Fact]
    public void ThrowIfWaitingFatal_ACrashLoop_SaysNothingAboutCredentials()
    {
        var status = Waiting("CrashLoopBackOff", "back-off restarting failed container");

        Watch(status, "acr-pull").Should().Throw<InvalidOperationException>()
            .Which.Message.Should().NotContain("acr-pull");
    }

    [Fact]
    public void ThrowIfWaitingFatal_AReadyContainer_DoesNotThrow()
    {
        var running = new V1ContainerStatus
        {
            Name = "toolchain",
            State = new V1ContainerState { Running = new V1ContainerStateRunning() }
        };

        Watch(running, "acr-pull").Should().NotThrow();
    }
}
