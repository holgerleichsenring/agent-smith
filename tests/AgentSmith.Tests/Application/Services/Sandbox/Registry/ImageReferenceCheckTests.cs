using System.Net;
using AgentSmith.Application.Models.Sandbox;
using AgentSmith.Application.Services.Sandbox.Registry;
using FluentAssertions;
using static AgentSmith.Tests.Sandbox.Registry.FakeRegistry;

namespace AgentSmith.Tests.Sandbox.Registry;

/// <summary>
/// 2026-10-12-50bdb: the registry's word on a stack.image — the observed invented tag is
/// missing with the real version pair offered, and nothing short of a 404 refuses.
/// </summary>
public sealed class ImageReferenceCheckTests
{
    private const string Invented = "sbtscala/scala-sbt:eclipse-temurin-25_1.11.7_2.13.18";
    private const string Published = "eclipse-temurin-25.0.1_8_1.11.7_2.13.18";

    private readonly FakeRegistry _registry = new();

    [Fact]
    public async Task Check_ChallengeThenTokenThen200_Exists()
    {
        _registry.Answer = DockerHub(_ => Respond(HttpStatusCode.OK));

        var verdict = await _registry.Check().CheckAsync("sbtscala/scala-sbt:" + Published, CancellationToken.None);

        verdict.State.Should().Be(ImageReferenceState.Exists);
        _registry.Requests.Select(r => r.RequestUri!.Host).Should().Equal(
            "registry-1.docker.io", "auth.docker.io", "registry-1.docker.io");
        _registry.Requests[1].RequestUri!.Query.Should().Contain("scope=repository%3Asbtscala%2Fscala-sbt%3Apull");
        _registry.Requests[2].Method.Should().Be(HttpMethod.Head);
    }

    [Fact]
    public async Task Check_ManifestHead_AcceptsOciIndexAndDockerList()
    {
        _registry.Answer = DockerHub(_ => Respond(HttpStatusCode.OK));

        await _registry.Check().CheckAsync("node:20-bookworm", CancellationToken.None);

        var accept = string.Join(",", _registry.Requests[^1].Headers.GetValues("Accept"));
        accept.Should().Contain("application/vnd.oci.image.index.v1+json")
            .And.Contain("application/vnd.docker.distribution.manifest.list.v2+json")
            .And.Contain("application/vnd.oci.image.manifest.v1+json")
            .And.Contain("application/vnd.docker.distribution.manifest.v2+json");
        _registry.Requests[^1].RequestUri!.AbsolutePath.Should().Be("/v2/library/node/manifests/20-bookworm");
    }

    [Fact]
    public async Task Check_404_IsMissingWithNearestTags()
    {
        _registry.Answer = DockerHub(request => request.Method == HttpMethod.Head
            ? Respond(HttpStatusCode.NotFound)
            : Tags("", "eclipse-temurin-25.0.4_7_1.13.0_2.13.18", Published, "graalvm-community-25_1.11.7_3.7.4"));

        var verdict = await _registry.Check().CheckAsync(Invented, CancellationToken.None);

        verdict.State.Should().Be(ImageReferenceState.Missing);
        verdict.TagsListed.Should().BeTrue();
        verdict.Candidates[0].Should().Be(Published);
        verdict.Detail.Should().Contain("registry-1.docker.io has no 'eclipse-temurin-25_1.11.7_2.13.18'");
        _registry.Requests[^1].Headers.Authorization!.Parameter.Should().Be(Token, "the tag list reuses the token");
    }

    [Fact]
    public async Task Check_404TagsUnlistable_IsMissingWithoutCandidates()
    {
        _registry.Answer = DockerHub(_ => Respond(HttpStatusCode.NotFound));

        var verdict = await _registry.Check().CheckAsync(Invented, CancellationToken.None);

        verdict.State.Should().Be(ImageReferenceState.Missing);
        verdict.TagsListed.Should().BeFalse();
        verdict.Candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task Check_401AfterToken_IsUnverified()
    {
        _registry.Answer = DockerHub(_ => Respond(HttpStatusCode.Unauthorized));

        var verdict = await _registry.Check().CheckAsync("someone/private:1.0", CancellationToken.None);

        verdict.State.Should().Be(ImageReferenceState.Unverified);
        verdict.Detail.Should().Contain("asks for credentials");
    }

    [Fact]
    public async Task Check_RateLimited_IsUnverified()
    {
        _registry.Answer = _ => Respond(HttpStatusCode.TooManyRequests);

        var verdict = await _registry.Check().CheckAsync("ghcr.io/org/tool:1", CancellationToken.None);

        verdict.State.Should().Be(ImageReferenceState.Unverified);
        verdict.Detail.Should().Contain("rate-limiting");
    }

    [Fact]
    public async Task Check_Timeout_IsUnverified()
    {
        // HttpClient reports its own timeout as a cancellation the caller did not ask for.
        _registry.Answer = _ => throw new TaskCanceledException("no answer");

        var verdict = await _registry.Check().CheckAsync("mcr.microsoft.com/dotnet/sdk:8.0", CancellationToken.None);

        verdict.State.Should().Be(ImageReferenceState.Unverified);
        verdict.Detail.Should().Contain("gave no answer");
    }

    [Fact]
    public async Task Check_ConnectionFailure_IsUnverified()
    {
        _registry.Answer = _ => throw new HttpRequestException(HttpRequestError.NameResolutionError);

        var verdict = await _registry.Check().CheckAsync("quay.io/org/tool:1", CancellationToken.None);

        verdict.State.Should().Be(ImageReferenceState.Unverified);
        verdict.Detail.Should().Contain("could not be reached");
    }

    [Fact]
    public async Task Check_PrivateHost_IsUnverifiedWithoutARequest()
    {
        _registry.Address = IPAddress.Parse("10.1.2.3");

        var verdict = await _registry.Check().CheckAsync("registry.internal.example:5000/team/tool:1", CancellationToken.None);

        verdict.State.Should().Be(ImageReferenceState.Unverified);
        verdict.Detail.Should().Contain("not looked up");
        _registry.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Check_CallerCancels_Propagates()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        _registry.Answer = _ => throw new TaskCanceledException();

        var act = () => _registry.Check().CheckAsync("node:20", cancelled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Check_Unparseable_IsUnverified()
    {
        var verdict = await _registry.Check().CheckAsync("not an image", CancellationToken.None);

        verdict.State.Should().Be(ImageReferenceState.Unverified);
        _registry.Requests.Should().BeEmpty();
    }
}
