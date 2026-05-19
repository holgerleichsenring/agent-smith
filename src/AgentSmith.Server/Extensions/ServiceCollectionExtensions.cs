namespace AgentSmith.Server.Extensions;

/// <summary>
/// Marker type for the Server composition root. The per-feature-set DI extension
/// methods (AddRedis, AddCoreDispatcherServices, AddServerCompositionOverrides,
/// AddLongRunningServices, AddIntentEngine / AddIntentHandlers, AddWebhookHandlers,
/// AddSlackAdapter / AddTeamsAdapter, AddSandbox / AddSandboxOptions /
/// AddSandboxGlobalConfig / AddOrchestratorGlobalConfig) live next to the services
/// they register — under <c>Services/&lt;feature&gt;/</c>. Program.cs composes them
/// directly via a flat call list.
/// </summary>
internal static class ServiceCollectionExtensions
{
}
