namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0407: one package cache a sandbox can be given — a directory that outlives the
/// run, plus the environment variables that make an ecosystem's tooling use it.
/// Ecosystem-agnostic by construction: the record says WHAT a cache is, never which
/// package manager the repo happens to use. The backend decides how the directory is
/// provided (a Docker volume today).
/// </summary>
/// <param name="Ecosystem">
/// Package-manager slug (<c>nuget</c>, <c>npm</c>, <c>pip</c>, …). Identity of the cache:
/// it names the backing volume and the log line, nothing branches on it.
/// </param>
/// <param name="MountPath">Where the cache directory appears inside the sandbox.</param>
/// <param name="Env">
/// Environment variables that point the ecosystem's tooling at the cache, as
/// name → absolute container path (e.g. <c>NUGET_PACKAGES</c> → <c>/pkgcache/nuget/packages</c>).
/// The env var is used rather than the tool's default home directory: it is explicit
/// and does not depend on which user the toolchain image runs as.
/// </param>
public sealed record PackageCacheMount(
    string Ecosystem,
    string MountPath,
    IReadOnlyDictionary<string, string> Env);
