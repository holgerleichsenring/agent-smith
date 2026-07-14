# Agent Smith demo sample project

A tiny C# service with one seeded, deterministic bug. `agent-smith demo`
materializes this project into a local git workspace and runs the real
`fix-bug` pipeline against it — no tracker, no remote, no webhook.

## The seeded bug

`src/Sample/PriceCalculator.cs` applies the 10% bulk discount only to orders
strictly **above** 100.00. The business rule (pinned by
`tests/Sample.Tests/PriceCalculatorTests.cs`) says orders of exactly 100.00
qualify too. One test fails until the boundary comparison is fixed:

```bash
dotnet test tests/Sample.Tests/Sample.Tests.csproj
# Total_ExactlyAtBulkThreshold_GetsDiscount: expected 90.00, got 100.00
```

The demo's inline ticket describes exactly this. When the run finishes you
should find a local commit that fixes the comparison and turns the test green.
