namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-10-13-9a59i: a decorator around a backend sandbox. A caller that needs a backend's own
/// marker (exit reader, liveness probe target) unwraps to <see cref="Inner"/> instead of every
/// decorator forwarding every marker.
/// </summary>
public interface ISandboxWrapper
{
    ISandbox Inner { get; }
}

public static class SandboxUnwrap
{
    /// <summary>The innermost sandbox behind any number of wrappers.</summary>
    public static ISandbox Backend(this ISandbox sandbox)
    {
        var current = sandbox;
        while (current is ISandboxWrapper wrapper) current = wrapper.Inner;
        return current;
    }
}
