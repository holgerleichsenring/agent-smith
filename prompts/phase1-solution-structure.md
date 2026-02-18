# Phase 1 - Step 1: Solution Structure

## Goal
.NET 8 Solution with all projects, correct references, and NuGet packages.
`dotnet build` must succeed without errors.

---

## Commands

```bash
# Solution erstellen
dotnet new sln -n AgentSmith

# Projekte erstellen
dotnet new classlib -n AgentSmith.Domain -o src/AgentSmith.Domain -f net8.0
dotnet new classlib -n AgentSmith.Contracts -o src/AgentSmith.Contracts -f net8.0
dotnet new classlib -n AgentSmith.Application -o src/AgentSmith.Application -f net8.0
dotnet new classlib -n AgentSmith.Infrastructure -o src/AgentSmith.Infrastructure -f net8.0
dotnet new console -n AgentSmith.Host -o src/AgentSmith.Host -f net8.0
dotnet new xunit -n AgentSmith.Tests -o tests/AgentSmith.Tests -f net8.0

# Zur Solution hinzufügen
dotnet sln add src/AgentSmith.Domain
dotnet sln add src/AgentSmith.Contracts
dotnet sln add src/AgentSmith.Application
dotnet sln add src/AgentSmith.Infrastructure
dotnet sln add src/AgentSmith.Host
dotnet sln add tests/AgentSmith.Tests

# Projekt-Referenzen
dotnet add src/AgentSmith.Contracts reference src/AgentSmith.Domain
dotnet add src/AgentSmith.Application reference src/AgentSmith.Contracts
dotnet add src/AgentSmith.Application reference src/AgentSmith.Domain
dotnet add src/AgentSmith.Infrastructure reference src/AgentSmith.Contracts
dotnet add src/AgentSmith.Infrastructure reference src/AgentSmith.Domain
dotnet add src/AgentSmith.Host reference src/AgentSmith.Application
dotnet add src/AgentSmith.Host reference src/AgentSmith.Infrastructure
dotnet add tests/AgentSmith.Tests reference src/AgentSmith.Domain
dotnet add tests/AgentSmith.Tests reference src/AgentSmith.Contracts
dotnet add tests/AgentSmith.Tests reference src/AgentSmith.Infrastructure

# NuGet Packages
dotnet add src/AgentSmith.Infrastructure package YamlDotNet
dotnet add src/AgentSmith.Host package Microsoft.Extensions.DependencyInjection
dotnet add src/AgentSmith.Host package Microsoft.Extensions.Logging.Console
dotnet add tests/AgentSmith.Tests package Moq
dotnet add tests/AgentSmith.Tests package FluentAssertions
```

## Directory Structure After Step 1

```
AgentSmith.sln
├── src/
│   ├── AgentSmith.Domain/
│   │   └── AgentSmith.Domain.csproj
│   ├── AgentSmith.Contracts/
│   │   └── AgentSmith.Contracts.csproj
│   ├── AgentSmith.Application/
│   │   └── AgentSmith.Application.csproj
│   ├── AgentSmith.Infrastructure/
│   │   └── AgentSmith.Infrastructure.csproj
│   └── AgentSmith.Host/
│       ├── AgentSmith.Host.csproj
│       └── Program.cs
├── tests/
│   └── AgentSmith.Tests/
│       └── AgentSmith.Tests.csproj
├── config/
└── prompts/
```

## Notes

- Delete all auto-generated `Class1.cs` files.
- Enable Nullable Reference Types in all projects (`<Nullable>enable</Nullable>`).
- Enable Implicit Usings (`<ImplicitUsings>enable</ImplicitUsings>`).
- Keep `Program.cs` in Host minimal for now: just `Console.WriteLine("Agent Smith")`.

## Result
```bash
dotnet build  # Must be error-free
```
