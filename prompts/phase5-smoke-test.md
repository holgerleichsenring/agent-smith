# Phase 5 - Step 3: Smoke Tests

## Goal
Ensure the DI container is built correctly and CLI parsing works.
No real API calls, only structural validation.

---

## DI Integration Test

```
File: tests/AgentSmith.Tests/Integration/DiRegistrationTests.cs
```

Test builds the complete DI container and verifies that all services are resolvable:

```csharp
[Fact]
public void AllServices_Resolvable()
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddAgentSmithInfrastructure();
    services.AddAgentSmithCommands();
    var provider = services.BuildServiceProvider();

    // Resolve all critical services
    provider.GetRequiredService<ProcessTicketUseCase>();
    provider.GetRequiredService<ICommandExecutor>();
    provider.GetRequiredService<IIntentParser>();
    provider.GetRequiredService<IPipelineExecutor>();
    provider.GetRequiredService<IConfigurationLoader>();
    provider.GetRequiredService<ITicketProviderFactory>();
    provider.GetRequiredService<ISourceProviderFactory>();
    provider.GetRequiredService<IAgentProviderFactory>();
}
```

---

## CLI Smoke Test

Verifies only the CLI argument structure:
- `--help` outputs help text and exits 0
- Without arguments outputs error and exits 1
- `--dry-run` with valid config parses intent without pipeline execution

---

## What is NOT Tested

- Real API calls (GitHub, Azure DevOps, Anthropic)
- Real Git operations
- Docker build (that is a CI/CD concern)
