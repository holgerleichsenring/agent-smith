namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-10-01-283df: whether a project's runs may render in a browser sandbox,
/// <c>projects.&lt;name&gt;.sandbox.browser</c>. Operator config, not a model choice: a run's
/// capacity is reserved before it starts, so the browser pod is either in the reservation or the
/// tool is not on the surface. Its sizing is the process-wide <see cref="BrowserSandboxConfig"/>.
/// </summary>
public sealed class ProjectBrowserConfig
{
    /// <summary>Off by default. On, render_reference joins the coding master and admission reserves a browser pod.</summary>
    public bool Enabled { get; set; }
}
