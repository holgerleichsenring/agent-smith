namespace AgentSmith.Server.Services.Pocs.Variables;

/// <summary>2026-11-23-933dg: one variable as the import and the variables route receive it — its name, whether it is
/// secret, and its value. Never echoed back.</summary>
internal sealed record PocVariableInput(string? Name, bool Secret, string? Value)
{
    public override string ToString() => $"{Name} ({(Secret ? "secret" : "plain")})";
}
