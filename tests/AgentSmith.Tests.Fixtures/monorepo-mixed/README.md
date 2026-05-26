# monorepo-mixed (p0161b fixture)

Three-context fixture used by p0161b integration tests to verify the
PipelineSandboxCoordinator fan-out + SandboxLanguageResolver discovery
flow against a real on-disk monorepo via LocalSourceProvider.

Layout:

```
.agentsmith/
  contexts/
    server/context.yaml   workdir: src/Server   lang: csharp
    client/context.yaml   workdir: src/Client   lang: typescript
    docs/context.yaml     workdir: docs         lang: markdown
src/
  Server/Program.cs       (minimal C# entry)
  Client/index.ts         (minimal TS module)
docs/
  README.md               (this file's sibling)
```

Discovery against this fixture must produce three RemoteContextDiscovery
entries with the matching workdir + language fields. PipelineSandboxCoordinator
then fans out three sandboxes keyed "server"/"client"/"docs" (single-repo
multi-context → bare context-name per SandboxKeyComposer).
