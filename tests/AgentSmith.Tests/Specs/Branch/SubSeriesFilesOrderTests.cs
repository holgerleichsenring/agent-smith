using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestSupport;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Branch;

/// <summary>
/// 2026-11-23-1e76a: the sub-series is read side by side — manifests, listings and specs — and answers as if read one by
/// one: the manifest's order, and the first failing spec in that order is the one named.
/// </summary>
public sealed class SubSeriesFilesOrderTests
{
    private const string Key = "azdo-19106";
    private const string Base = "2026-11-23-0a0a";

    private static string Manifest(params string[] ids) => $"ticket: {Key}\nspecs:\n{string.Concat(ids.Select(i => $"- {i}\n"))}";

    private static string Spec(string id) => $"spec: {id}\ngoal: g\ndone:\n  - d\n";

    [Fact]
    public async Task ReadAsync_ReadsLandingLastFirst_KeepsTheManifestOrder()
    {
        var branch = new SpecBranchFiles();
        branch.SeedSeries("2026-11-23-9e9e", "ticket: azdo-77\nspecs:\n- 2026-11-23-9e9ea\n", new Dictionary<string, string>());
        branch.SeedSeries(Base, Manifest($"{Base}a", $"{Base}b", $"{Base}c"), new Dictionary<string, string>
        {
            [$"{Base}a-first"] = Spec($"{Base}a"), [$"{Base}b-second"] = Spec($"{Base}b"), [$"{Base}c-third"] = Spec($"{Base}c"),
        });

        var read = await NewestFirstAsync(branch);

        read.State.Should().Be(SubSeriesState.Found, read.Why);
        read.Specs.Select(s => s.SpecId).Should().Equal($"{Base}a", $"{Base}b", $"{Base}c");
    }

    [Fact]
    public async Task ReadAsync_TwoSpecsFail_NamesTheFirstInTheManifest()
    {
        var branch = new SpecBranchFiles();
        branch.SeedSeries(Base, Manifest($"{Base}a", $"{Base}b", $"{Base}c"), new Dictionary<string, string>
        {
            [$"{Base}a-first"] = Spec($"{Base}a"), [$"{Base}c-third"] = " ",
        });

        var read = await NewestFirstAsync(branch);

        read.State.Should().Be(SubSeriesState.Unreadable);
        read.Why.Should().Contain($"spec {Base}b").And.NotContain("third");
    }

    // Every read is held until the reader has asked all it can without an answer; then the newest answers, and again.
    private static async Task<SubSeriesRead> NewestFirstAsync(SpecBranchFiles branch)
    {
        var files = new HeldFiles(branch);
        var read = SeriesDoubles.SubSeries().ReadAsync(files, "api", new TicketKey(Key), default);
        while (!read.IsCompleted)
        {
            await TestWaits.UntilAsync(() => files.Held > 0 || read.IsCompleted, "a read or the answer");
            var held = files.Held;
            if (await TestWaits.StaysAsync(() => files.Held == held)) files.ReleaseNewest();
        }
        return await read;
    }

    private sealed class HeldFiles(ISandboxFileReader inner) : ISandboxFileReader
    {
        private readonly Lock _gate = new();
        private readonly List<TaskCompletionSource> _held = [];

        public int Held
        {
            get { lock (_gate) return _held.Count; }
        }

        public void ReleaseNewest()
        {
            TaskCompletionSource released;
            lock (_gate)
            {
                if (_held.Count == 0) return;
                released = _held[^1];
                _held.RemoveAt(_held.Count - 1);
            }
            released.SetResult();
        }

        private async Task<T> HeldAsync<T>(Func<Task<T>> read)
        {
            var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate) _held.Add(held);
            await held.Task;
            return await read();
        }

        public Task<bool> ExistsAsync(string path, CancellationToken ct) => HeldAsync(() => inner.ExistsAsync(path, ct));

        public Task<string?> TryReadAsync(string path, CancellationToken ct) => HeldAsync(() => inner.TryReadAsync(path, ct));

        public Task<string> ReadRequiredAsync(string path, CancellationToken ct) => HeldAsync(() => inner.ReadRequiredAsync(path, ct));

        public Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken ct) =>
            HeldAsync(() => inner.ListAsync(path, maxDepth, ct));

        public Task WriteAsync(string path, string content, CancellationToken ct) => inner.WriteAsync(path, content, ct);
    }
}
