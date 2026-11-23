using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Source.BranchWrites;

/// <summary>
/// 2026-11-23-c81ea: only a probe's "absent" drops a deletion; a probe that throws fails the write naming the path, and
/// nothing but absent deletions answers AlreadyHeld. Writes are never probed.
/// </summary>
public sealed class AbsentDeletionFilterTests
{
    private readonly AbsentDeletionFilter _sut = new(NullLogger.Instance);
    private readonly List<string> _probed = [];

    [Fact]
    public async Task FilterAsync_AbsentDeletion_DroppedWithANote_WritesKeptUnprobed()
    {
        var result = await _sut.FilterAsync(
            [new RepoFile("a", "x"), RepoFile.Deletion("gone"), RepoFile.Deletion("here")], Probe(present: "here"), default);

        result.Refusal.Should().BeNull();
        result.Files.Select(f => f.Path).Should().Equal("a", "here");
        result.Notes.Should().ContainSingle().Which.Should().Contain("gone");
        _probed.Should().Equal("gone", "here");
        result.WithNotes(BranchWriteResult.Ok("s")).Notes.Should().Equal(result.Notes);
    }

    [Fact]
    public async Task FilterAsync_ProbeThrows_RefusesNamingThePath()
    {
        var result = await _sut.FilterAsync(
            [RepoFile.Deletion("deploy/x.yaml")], (_, _) => throw new HttpRequestException("503"), default);

        result.Refusal!.Written.Should().BeFalse();
        result.Refusal.AlreadyHeld.Should().BeFalse();
        result.Refusal.Error.Should().Contain("deploy/x.yaml").And.Contain("503");
    }

    [Fact]
    public async Task FilterAsync_EveryDeletionAbsent_RefusesAsAlreadyHeld()
    {
        var result = await _sut.FilterAsync([RepoFile.Deletion("gone")], Probe(present: null), default);

        result.Refusal!.AlreadyHeld.Should().BeTrue();
        result.Refusal.Notes.Should().ContainSingle();
    }

    [Fact]
    public async Task WithNotes_NoNotes_LeavesTheResultAlone()
    {
        var result = await _sut.FilterAsync([new RepoFile("a", "x")], Probe(present: null), default);

        result.WithNotes(BranchWriteResult.Ok("s")).Notes.Should().BeNull();
    }

    private Func<string, CancellationToken, Task<bool>> Probe(string? present) => (path, _) =>
    {
        _probed.Add(path);
        return Task.FromResult(path == present);
    };
}
