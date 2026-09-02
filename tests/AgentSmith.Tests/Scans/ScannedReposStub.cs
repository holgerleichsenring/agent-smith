using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Tests.Scans;

/// <summary>
/// 2026-09-01-85b2: a run whose repositories are separate checkouts, each with its own
/// files. The evidence check used to read the single default sandbox while the scan master
/// addressed all of them, so every finding in a second repository resolved against nothing.
/// </summary>
internal sealed class ScannedReposStub(Dictionary<ISandbox, ISandboxFileReader> byRepo)
    : ISandboxFileReaderFactory
{
    public ISandboxFileReader Create(ISandbox sandbox) => byRepo[sandbox];
}
