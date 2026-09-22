namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// Editable studio view of one project. <see cref="Agent"/>, <see cref="Tracker"/>
/// and <see cref="Repos"/> are catalog references (names) — the referential
/// validator rejects any that is not present in the catalog, so a broken wiring
/// can never be persisted. p0345c truth-fix: the field previously labelled
/// <c>trigger</c> always mapped the raw <c>pipeline:</c> key — it is now named
/// <see cref="Pipeline"/> on the wire. <see cref="Resolution"/> is the flat
/// p0281b routing shorthand (strategy + value); upsert validates the strategy
/// against the known set served by <c>GET /api/config/capabilities</c>.
/// </summary>
public sealed record ProjectEntity(
    string Id,
    string Agent,
    string Tracker,
    IReadOnlyList<string> Repos,
    string? Pipeline,
    IReadOnlyList<string> Pipelines,
    ProjectResolution? Resolution = null,
    // p0392: what PipelineConfigResolver picks when a ticket carries no routing label.
    // ProjectConfigNormalizer disables the project when it names an undeclared pipeline,
    // and the studio could neither set it nor see why the project had stopped.
    string? DefaultPipeline = null,
    // 2026-09-13-5fa0: NULLABLE on purpose, and the patch writes it only when it is not
    // null. RawProjectPatch.Apply assigns Repos unconditionally, which is why every
    // studio save already drops default_branch and consumes; a client that constructs a
    // ProjectEntity without knowing this field — the dashboard's blankEntity does — would
    // wipe a declaration the same way. Absent means "I have nothing to say about
    // templates", not "there are none".
    IReadOnlyList<TemplateReference>? Templates = null,
    // 2026-09-22-6968: the five SCALAR per-project sandbox overrides. Nullable for the same
    // reason Templates is: absent means "I was not told", and the patch leaves the stored
    // block untouched. A form that shows the block sends all five, so a null INSIDE a sent
    // block means cleared-to-inherit.
    ProjectSandbox? Sandbox = null)
{
    public ProjectEntity() : this(string.Empty, string.Empty, string.Empty, [], null, []) { }
}

/// <summary>
/// How the webhook/poll dispatch decides this project owns an incoming ticket:
/// a strategy (tag | area_path | repo | to_address) plus the value to match.
/// </summary>
public sealed record ProjectResolution(string Strategy, string Value);
