namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-08-25-0d01: reads the protocol version a sandbox agent stamps on what it sends
/// back, and reports one this server does not speak.
/// </summary>
public interface IWireProtocolWatcher
{
    /// <summary>
    /// Called with the schema version of every message that arrives from an agent. Records
    /// an advisory finding the first time one falls outside the supported window; never
    /// refuses the message, and never throws.
    /// </summary>
    void Observe(int schemaVersion);
}
