namespace AgentSmith.Contracts.Constants;

/// <summary>
/// 2026-10-01-283de: the browser image this repository builds and publishes beside the carrier,
/// under the same registry and the same tag. It is a TOOLCHAIN image — Playwright's Chromium with
/// the render script baked in — that the carrier's agent binary is injected into like any other.
/// </summary>
public static class BrowserImageDefaults
{
    /// <summary>Image name (sans registry, sans tag) of the browser sandbox image published by this project's CI.</summary>
    public const string SandboxBrowserImageName = "agent-smith-sandbox-browser";

    /// <summary>Where the image bakes its render script; node is handed this path and one JSON file.</summary>
    public const string RenderScriptPath = "/opt/agentsmith/render.mjs";
}
