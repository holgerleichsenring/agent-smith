using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0299: a no-op file-reader factory for handler/git tests that now exercise
/// SandboxGitOperations.ConsolidateSecondarySandboxesAsync (it writes the consolidation
/// patch through the reader). Single-sandbox tests never reach the write, but multi-repo /
/// multi-sandbox setups do — a bare Mock.Of returns a null reader and NREs.
/// </summary>
internal sealed class StubSandboxFileReaderFactory : ISandboxFileReaderFactory
{
    public ISandboxFileReader Create(ISandbox sandbox) => new StubReader();

    private sealed class StubReader : ISandboxFileReader
    {
        public Task<bool> ExistsAsync(string path, CancellationToken ct) => Task.FromResult(false);
        public Task<string?> TryReadAsync(string path, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string> ReadRequiredAsync(string path, CancellationToken ct) => Task.FromResult(string.Empty);
        public Task WriteAsync(string path, string content, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
