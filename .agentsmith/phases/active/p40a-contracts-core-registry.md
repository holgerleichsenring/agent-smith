# Phase 40a: Contracts Extensions, Infrastructure.Core & ProviderRegistry

## Goal

Lay the foundation for provider decomposition: new interfaces in Contracts,
a generic ProviderRegistry replacing hardcoded factory switches, and
Infrastructure.Core containing all non-provider infrastructure services.

This is the first of four sub-phases (p40a-d) that decompose
`AgentSmith.Infrastructure` into independently deployable provider projects.

---

## Contracts Additions (additive only)

### AttachmentRef (Domain)

```csharp
// src/AgentSmith.Domain/Entities/AttachmentRef.cs
public sealed record AttachmentRef(
    string Uri,
    string FileName,
    string MimeType,
    long? SizeBytes = null);
```

### ITypedProvider (Contracts)

```csharp
// src/AgentSmith.Contracts/Providers/ITypedProvider.cs
public interface ITypedProvider
{
    string ProviderType { get; }
}
```

Existing interfaces extend ITypedProvider (non-breaking, they already have ProviderType):
- `ITicketProvider : ITypedProvider`
- `ISourceProvider : ITypedProvider`
- `IAgentProvider : ITypedProvider`

### ITicketProvider — GetAttachmentRefsAsync (default method)

```csharp
Task<IReadOnlyList<AttachmentRef>> GetAttachmentRefsAsync(
    TicketId ticketId,
    CancellationToken cancellationToken = default)
    => Task.FromResult<IReadOnlyList<AttachmentRef>>(Array.Empty<AttachmentRef>());
```

Default implementation returns empty list — no existing provider breaks.

### IStorageReader (Contracts)

```csharp
// src/AgentSmith.Contracts/Providers/IStorageReader.cs
public interface IStorageReader
{
    bool CanHandle(AttachmentRef attachmentRef);

    Task<Stream> ReadAsync(
        AttachmentRef attachmentRef,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        AttachmentRef attachmentRef,
        CancellationToken cancellationToken = default);
}
```

No SupportedSchemes. Each reader decides via `CanHandle(AttachmentRef)` —
can inspect scheme, host, path pattern, anything. Registry does
`FirstOrDefault(r => r.CanHandle(ref))`.

### IStorageWriter (Contracts)

```csharp
// src/AgentSmith.Contracts/Providers/IStorageWriter.cs
public interface IStorageWriter : ITypedProvider
{
    Task<AttachmentRef> WriteAsync(
        string fileName,
        Stream content,
        string mimeType,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        AttachmentRef attachmentRef,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
```

### New ContextKeys

```csharp
public const string Attachments = "Attachments";
```

---

## Infrastructure.Core

New project: `src/AgentSmith.Infrastructure.Core/`

References: `AgentSmith.Contracts`, `AgentSmith.Domain`
NuGet: YamlDotNet, Microsoft.Extensions.* (no provider-specific packages)

### What moves from Infrastructure to Infrastructure.Core

- `YamlConfigurationLoader` (IConfigurationLoader)
- `SecretsProvider`
- `ContextValidator` (IContextValidator)
- `ContextGenerator` (IContextGenerator)
- `CodeMapGenerator` (ICodeMapGenerator)
- `CodingPrinciplesGenerator` (ICodingPrinciplesGenerator)
- `YamlSkillLoader` (ISkillLoader)
- `ProjectDetector` (IProjectDetector)
- `RepoSnapshotCollector` (IRepoSnapshotCollector)
- `DotNetLanguageDetector`, `TypeScriptLanguageDetector`, `PythonLanguageDetector` (ILanguageDetector)

**Rule:** No external vendor NuGet (Anthropic, Octokit, LibGit2Sharp, etc.) in this project.

### New: StorageReaderRegistry

```csharp
// src/AgentSmith.Infrastructure.Core/Services/StorageReaderRegistry.cs
public sealed class StorageReaderRegistry(
    IEnumerable<IStorageReader> readers,
    ILogger<StorageReaderRegistry> logger)
{
    public IStorageReader Resolve(AttachmentRef attachmentRef) =>
        readers.FirstOrDefault(r => r.CanHandle(attachmentRef))
            ?? throw new ConfigurationException(
                $"No IStorageReader can handle '{attachmentRef.Uri}'.");
}
```

### New: ProviderRegistry<T>

```csharp
// src/AgentSmith.Infrastructure.Core/Services/ProviderRegistry.cs
public sealed class ProviderRegistry<TProvider>(
    IEnumerable<TProvider> providers)
    where TProvider : ITypedProvider
{
    private readonly IReadOnlyDictionary<string, TProvider> _map =
        providers.ToDictionary(
            p => p.ProviderType.ToLowerInvariant(),
            StringComparer.OrdinalIgnoreCase);

    public TProvider Resolve(string providerType) =>
        _map.TryGetValue(providerType, out var provider)
            ? provider
            : throw new ConfigurationException(
                $"No {typeof(TProvider).Name} registered for type '{providerType}'.");
}
```

### Factory migration

Existing factories become thin wrappers:

```csharp
public sealed class TicketProviderFactory(ProviderRegistry<ITicketProvider> registry)
    : ITicketProviderFactory
{
    public ITicketProvider Create(TicketConfig config) =>
        registry.Resolve(config.Type);
}
```

Config no longer passed to factory — providers receive their config via
`IOptions<T>` at construction time.

### DI Registration

```csharp
public static IServiceCollection AddAgentSmithCore(this IServiceCollection services)
{
    // All non-provider infrastructure services
    services.AddSingleton<IConfigurationLoader, YamlConfigurationLoader>();
    services.AddSingleton<IContextValidator, ContextValidator>();
    // ... (everything currently in Infrastructure.ServiceCollectionExtensions)

    // Generic registries
    services.AddSingleton(typeof(ProviderRegistry<>));
    services.AddSingleton<StorageReaderRegistry>();

    return services;
}
```

---

## Files to Create

- `src/AgentSmith.Domain/Entities/AttachmentRef.cs`
- `src/AgentSmith.Contracts/Providers/ITypedProvider.cs`
- `src/AgentSmith.Contracts/Providers/IStorageReader.cs`
- `src/AgentSmith.Contracts/Providers/IStorageWriter.cs`
- `src/AgentSmith.Infrastructure.Core/AgentSmith.Infrastructure.Core.csproj`
- `src/AgentSmith.Infrastructure.Core/Services/StorageReaderRegistry.cs`
- `src/AgentSmith.Infrastructure.Core/Services/ProviderRegistry.cs`
- `src/AgentSmith.Infrastructure.Core/Extensions/ServiceCollectionExtensions.cs`

## Files to Modify

- `src/AgentSmith.Contracts/Providers/ITicketProvider.cs` — extend ITypedProvider, add GetAttachmentRefsAsync
- `src/AgentSmith.Contracts/Providers/ISourceProvider.cs` — extend ITypedProvider
- `src/AgentSmith.Contracts/Providers/IAgentProvider.cs` — extend ITypedProvider
- `src/AgentSmith.Contracts/Commands/ContextKeys.cs` — add Attachments
- `src/AgentSmith.Host/` — reference Infrastructure.Core instead of Infrastructure
- `AgentSmith.sln` — add Infrastructure.Core project

## Files to Move (Infrastructure -> Infrastructure.Core)

- All services listed above (YamlConfigurationLoader, SecretsProvider, etc.)
- All factory implementations (updated to use ProviderRegistry<T>)

---

## Tests

- `ProviderRegistryTests` — resolve by type, missing type throws
- `StorageReaderRegistryTests` — resolve by CanHandle, no match throws
- `FactoryMigrationTests` — factories delegate to registry
- All existing 426+ tests remain green

---

## Estimation

~200 lines new code, ~150 lines moved/refactored. No behavioral changes.
