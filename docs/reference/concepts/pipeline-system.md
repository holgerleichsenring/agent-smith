# Pipeline system

Everything Agent Smith does is a pipeline: an ordered list of commands executed in sequence.

## Commands and handlers

Each pipeline step is a **command** with a matching **handler**. The command names what needs to happen, the handler does it. Handlers receive a typed context object and return a `CommandResult`, success or failure with a message. A failed step stops the pipeline, but the `WriteRunResult`, `CommitAndPR` and `PrCrossLink` steps still ahead of it run anyway, so a failed run still leaves its record and its partial work on the branch.

This is the `code` pipeline as it is defined in `PipelinePresets.Code.cs`:

```
Pipeline: code
├── LoadCatalog
├── PipelineNameInitializer
├── FetchTicket
├── ScopeRepos
├── CheckoutSource
├── RunPreflight
├── SetupRegistryAuth
├── BootstrapCheck
├── BootstrapGate
├── LoadCodingPrinciples
├── LoadMemoryIndex
├── LoadContext
├── AnalyzeCode
├── DeriveSpec
├── SpecHandback
├── PhaseSpecGate
├── EnsurePrerequisites
├── ProbeTarget
├── PhaseSequence
├── WriteRunResult
├── CommitAndPR
└── PrCrossLink
```

## Steps that expand at runtime

A preset is fixed, but a handler can splice further commands into the running pipeline, directly after itself. Two do:

- `PhaseSequence` in `code` inserts one block per phase the run derived: `SelectPhase`, `CheckPhasePremises`, `AgenticMaster`, `MasterOpenQuestions`, `CommitPhaseWork`, `VerifyPhase`, `ReviewPhaseDiff`, `WritePhaseRecord`. How many blocks a ticket becomes is decided during the run. See [The code pipeline](../pipelines/fix-and-feature.md).
- `BootstrapDispatch` in `init-project` inserts one `BootstrapRound` per repository and discovered component.

## Pipeline presets

The presets live in `src/backend/AgentSmith.Contracts/Commands/PipelinePresets.*.cs`, one file per preset. Each preset that needs judgement hands it to one master skill at its `AgenticMaster` step. [Pipelines](../pipelines/index.md) lists them with what starts them and which master they load, and shows the control flow generated from these files.

A project chooses which presets it hosts with `pipelines:` in its configuration; it cannot define its own command sequence. A pipeline name that is not a preset fails the run when it starts.

## PipelineContext

All commands of a run share a `PipelineContext`, a key-value store that flows through the pipeline. Commands read from and write to it:

```csharp
// Write
pipeline.Set(ContextKeys.RunId, runId);

// Read
pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set);
```

This is how data flows between steps without the handlers knowing about each other.
