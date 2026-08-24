namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// p0504: the on-disk shape of <c>profiles/&lt;name&gt;/profile.yaml</c> in the
/// skills catalog. Read with the same underscored naming convention the rest of
/// the catalog uses, so <c>compatible_images</c> maps onto CompatibleImages.
/// </summary>
internal sealed class DomainProfileYaml
{
    public string? Name { get; set; }

    public string? Image { get; set; }

    public List<string>? CompatibleImages { get; set; }

    public List<CommandEntry>? Verify { get; set; }

    internal sealed class CommandEntry
    {
        public string? Stage { get; set; }

        public string? Command { get; set; }

        /// <summary>p0513: <c>when_present</c> — the path the command needs to exist.</summary>
        public string? WhenPresent { get; set; }
    }
}
