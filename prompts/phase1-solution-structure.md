# Phase 1 - Schritt 1: Solution Structure

## Ziel
.NET 8 Solution mit allen Projekten, korrekten Referenzen und NuGet Packages.
`dotnet build` muss fehlerfrei durchlaufen.

---

## Befehle

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

## Verzeichnisstruktur nach Schritt 1

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

## Hinweise

- Alle auto-generierten `Class1.cs` Dateien löschen.
- Nullable Reference Types in allen Projekten aktivieren (`<Nullable>enable</Nullable>`).
- Implicit Usings aktivieren (`<ImplicitUsings>enable</ImplicitUsings>`).
- `Program.cs` in Host vorerst minimal: nur `Console.WriteLine("Agent Smith")`.

## Ergebnis
```bash
dotnet build  # Muss fehlerfrei sein
```
