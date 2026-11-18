namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-11-17-f3a8d: a secret value on its way from the admin area's field to the secret store. It says nothing
/// about itself: no public property a serializer could write, and a ToString that names no value, so a log line, a
/// checkpoint or an event that touches it carries "[secret]". Only the store reads it.
/// </summary>
public sealed class SecretInput(string value)
{
    public string Reveal() => value;

    public int Length => value.Length;

    public override string ToString() => "[secret]";
}
