namespace AgentSmith.Domain.Models;

public enum ModuleRole { Production, Test, Tool, Generated, Other }

public sealed record Module(
    string Path,
    ModuleRole Role,
    IReadOnlyList<string> DependsOn);
