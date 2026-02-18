# Phase 10: Headless Mode - Implementation Details

## Overview
Container and CI environments cannot use interactive prompts. The `--headless` flag
makes Agent Smith fully autonomous by auto-approving plans without user confirmation.

---

## CLI Option (Host Layer)

Add `--headless` to System.CommandLine in `Program.cs`:

```csharp
var headlessOption = new Option<bool>(
    "--headless", "Run without interactive prompts (auto-approve plans)");
```

Pass the value to `ProcessTicketUseCase.ExecuteAsync()`.

---

## ContextKeys (Contracts Layer)

```csharp
// Commands/ContextKeys.cs
public const string Headless = "Headless";
```

---

## ProcessTicketUseCase (Application Layer)

Add `bool headless = false` parameter to `ExecuteAsync`.
Store in PipelineContext:

```csharp
pipeline.Set(ContextKeys.Headless, headless);
```

---

## ApprovalHandler (Application Layer)

Before prompting the user, check the headless flag:

```csharp
var headless = context.Pipeline.TryGet<bool>(ContextKeys.Headless, out var h) && h;
if (headless)
{
    logger.LogInformation("Headless mode: auto-approving plan");
    approved = true;
}
```

If headless, skip `Console.ReadLine()` and auto-approve.

---

## Security Consideration
Headless mode is explicitly opt-in via CLI flag. In non-headless mode,
the existing interactive approval flow remains unchanged.
