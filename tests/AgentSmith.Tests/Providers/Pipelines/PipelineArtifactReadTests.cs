using System.IO.Compression;
using System.Text;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Pipelines;
using FluentAssertions;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Moq;

namespace AgentSmith.Tests.Providers.Pipelines;

/// <summary>2026-11-23-933dd: one file of a run's artifact on each host — read as text, too large with its size, or
/// absent as null.</summary>
public sealed class PipelineArtifactReadTests
{
    private static readonly PipelineArtifact Digest = new("poc-image", "push", "digest.txt");
    private const string Sha = "sha256:1111111111111111111111111111111111111111111111111111111111111111";
    private readonly FakePipelineHost _host = new();

    [Fact]
    public async Task GitHub_ReadsTheFileOutOfTheNamedArtifactsZip_TooLargeAndAbsentToo()
    {
        _host.On("GET", "/actions/runs/7/artifacts?name=poc-image&per_page=100",
                """{"artifacts":[{"id":3,"name":"poc-image","expired":false,"archive_download_url":"https://api.github.com/zip/3"}]}""")
            .On("GET", "/actions/runs/7/artifacts?name=other&per_page=100", """{"total_count":0,"artifacts":[]}""")
            .OnBytes("GET", "/zip/3", Zip(("digest.txt", Sha)));
        var runner = new GitHubPipelineRunner(_host.Http(), GitHubRepoAddress.Parse("https://github.com/example/poc-src"), "main");

        (await runner.ReadArtifactTextAsync("7", Digest, 256, default))!.Text.Should().Be(Sha);
        (await runner.ReadArtifactTextAsync("7", Digest, 10, default)).Should().Be(PipelineArtifactText.TooLarge(Sha.Length));
        (await runner.ReadArtifactTextAsync("7", Digest with { Name = "other" }, 256, default)).Should().BeNull();
    }

    [Fact]
    public async Task GitLab_ReadsTheFileOfTheNamedJobsArtifacts()
    {
        _host.On("GET", "/pipelines/7/jobs?per_page=100", """[{"id":40,"name":"push"},{"id":41,"name":"push"},{"id":39,"name":"scan"}]""")
            .OnBytes("GET", "/jobs/41/artifacts/digest.txt", Encoding.UTF8.GetBytes(Sha));
        var runner = new GitLabPipelineRunner(_host.Http(), "https://gitlab.example/api/v4/projects/1", "main");

        (await runner.ReadArtifactTextAsync("7", Digest, 256, default))!.Text.Should().Be(Sha);
        (await runner.ReadArtifactTextAsync("7", Digest, 10, default))!.IsTooLarge.Should().BeTrue();
        (await runner.ReadArtifactTextAsync("7", Digest with { File = "absent.txt" }, 256, default)).Should().BeNull();
        (await runner.ReadArtifactTextAsync("7", Digest with { Job = "deploy" }, 256, default)).Should().BeNull();
    }

    [Fact]
    public async Task Azure_ReadsTheFileOutOfTheBuildArtifactsZip()
    {
        var build = new Mock<BuildHttpClient>(new Uri("https://localhost/fake"), new VssCredentials(new VssBasicCredential(string.Empty, "fake")));
        build.Setup(c => c.GetArtifactsAsync("demo", 7, It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new BuildArtifact { Name = "poc-image" }]);
        build.Setup(c => c.GetArtifactContentZipAsync("demo", 7, "poc-image", It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(Zip(("poc-image/digest.txt", Sha))));
        var runner = new AzurePipelineRunner(_ => Task.FromResult(build.Object), "demo", "https://dev.azure.com/example");

        (await runner.ReadArtifactTextAsync("7", Digest, 256, default))!.Text.Should().Be(Sha);
        (await runner.ReadArtifactTextAsync("7", Digest, 10, default))!.SizeBytes.Should().Be(Sha.Length);
        (await runner.ReadArtifactTextAsync("7", Digest with { Name = "poc-scan-report" }, 256, default)).Should().BeNull();
    }

    private static byte[] Zip(params (string Path, string Text)[] files)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (path, text) in files)
            {
                using var entry = zip.CreateEntry(path).Open();
                entry.Write(Encoding.UTF8.GetBytes(text));
            }
        return buffer.ToArray();
    }
}
