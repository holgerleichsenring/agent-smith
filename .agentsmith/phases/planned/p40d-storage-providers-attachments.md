# Phase 40d: Storage Providers & LoadAttachmentsCommand

## Goal

Implement storage providers (Disk, SharePoint, Blob) and the
`LoadAttachmentsCommand` pipeline step. After this phase, the legal analysis
pipeline can ingest documents from any storage backend, and the old
`AgentSmith.Infrastructure` project can be deleted.

---

## Storage Provider Projects

### AgentSmith.Providers.Storage.Disk

```
References: AgentSmith.Contracts, AgentSmith.Domain
NuGet:      (none)
```

Contains:
- `DiskStorageReader` — IStorageReader
  - `CanHandle`: checks for `file://` scheme or local path pattern
  - Reads from local filesystem
- `DiskStorageWriter` — IStorageWriter
  - `ProviderType = "disk"`
  - Writes to configurable local directory

Registration: `services.AddDiskStorage(configuration);`

### AgentSmith.Providers.Storage.SharePoint

```
References: AgentSmith.Contracts, AgentSmith.Domain
NuGet:      Microsoft.Graph
```

Contains:
- `SharePointStorageReader` — IStorageReader
  - `CanHandle`: checks for `https://` scheme AND `*.sharepoint.com` host
  - Reads via Microsoft Graph API
- `SharePointStorageWriter` — IStorageWriter
  - `ProviderType = "sharepoint"`

Registration: `services.AddSharePointStorage(configuration);`

### AgentSmith.Providers.Storage.Blob

```
References: AgentSmith.Contracts, AgentSmith.Domain
NuGet:      Azure.Storage.Blobs
```

Contains:
- `BlobStorageReader` — IStorageReader
  - `CanHandle`: checks for `blob://` scheme or `*.blob.core.windows.net` host
- `BlobStorageWriter` — IStorageWriter
  - `ProviderType = "blob"`

Registration: `services.AddBlobStorage(configuration);`

---

## LoadAttachmentsCommand (Application)

New optional pipeline step that fetches attachment content and stores it
in the PipelineContext for downstream handlers.

### Command and Handler

```csharp
// CommandNames
public const string LoadAttachments = "LoadAttachmentsCommand";

// Context
public sealed record LoadAttachmentsContext(
    PipelineContext Pipeline) : ICommandContext;

// Handler
public sealed class LoadAttachmentsHandler(
    StorageReaderRegistry storageReaderRegistry,
    ILogger<LoadAttachmentsHandler> logger) : ICommandHandler<LoadAttachmentsContext>
{
    public async Task<CommandResult> ExecuteAsync(
        LoadAttachmentsContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<IReadOnlyList<AttachmentRef>>(
                ContextKeys.Attachments, out var refs) || refs is null || refs.Count == 0)
        {
            return CommandResult.Ok("No attachments to load");
        }

        foreach (var attachmentRef in refs)
        {
            var reader = storageReaderRegistry.Resolve(attachmentRef);
            using var stream = await reader.ReadAsync(attachmentRef, cancellationToken);
            // Store content in workspace or pipeline context
        }

        return CommandResult.Ok($"Loaded {refs.Count} attachments");
    }
}
```

### Pipeline Integration

The legal analysis pipeline becomes:

```
AcquireSource > BootstrapDocument > LoadAttachments > LoadDomainRules >
Triage > [SkillRounds] > ConvergenceCheck > CompileDiscussion > DeliverOutput
```

`LoadAttachments` is optional — coding pipelines skip it.

---

## Config Extension

```yaml
# agentsmith.yml
projects:
  legal-contracts:
    tickets:
      type: Jira
    agent:
      type: Claude
    storage:
      reader: sharepoint
      writer: sharepoint
      sharepoint:
        site_url: https://mycompany.sharepoint.com/sites/legal
        library: Contracts
    pipeline: legal-analysis

  code-project:
    tickets:
      type: GitHub
    source:
      type: GitHub
    agent:
      type: Claude
    storage:
      reader: disk
      writer: disk
      disk:
        inbox: ./inbox
        outbox: ./outbox
```

Storage config is optional. Default: disk.

---

## Infrastructure Cleanup

After all providers are extracted (p40a-d complete):
- `AgentSmith.Infrastructure` contains zero provider classes
- `AgentSmith.Infrastructure` contains zero non-provider services (moved to Core)
- Project can be deleted from solution
- Or kept as meta-package that references all built-in providers for convenience

---

## Definition of Done

- [ ] 3 storage provider projects created and building
- [ ] `LoadAttachmentsCommand` + handler implemented
- [ ] Legal analysis pipeline updated to include LoadAttachments
- [ ] Storage config in agentsmith.yml parsed and wired
- [ ] `AgentSmith.Infrastructure` is empty or deleted
- [ ] All tests pass

---

## Estimation

~150 lines per storage provider, ~80 lines for LoadAttachmentsHandler.
~550 lines total new code.
