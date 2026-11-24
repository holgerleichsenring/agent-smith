"use client";

import type {
  InheritedSandbox,
  InheritedSandboxProjection,
  ProjectSandbox,
  ProjectSandboxStructured,
  ResolvedValue,
  StudioProject,
} from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";
import { CheckField, MapField, NumberField, TextField } from "./formFields";
import { SandboxResourceGroup } from "./SandboxResourceGroup";
import { SandboxSecretsBlock } from "./SandboxSecretsBlock";

// 2026-09-22-6968: the project form's sixth tab — the SCALAR per-project sandbox
// overrides, none of which was editable anywhere in the product before this. Every one is
// null-means-inherit, so every control has to say what CLEARING it restores; the
// placeholder is the counterfactual value the server computes (what this project would get
// with its sandbox block empty), never its own current value.
//
// 2026-09-22-6c46 added the structured three below the scalars, and each answers the
// inheritance question its own way: the cpu/memory group names the LAYER that would answer
// (four layers, one of them a document written per run), the image map inherits PER KEY from
// a code table, and the pod's secrets inherit NOTHING and say so.
//
// 2026-09-23-2446 added the hold window as the sixth scalar. It is the one control here
// whose placeholder is LIVE, because its effect is: a reaper resolves the window through the
// configuration loader on every scan, so an edit to the process-wide value is in force
// within a scan interval and a placeholder frozen at composition would disagree with it from
// that moment on. The other five reach their consumers at run start or after a restart, so a
// frozen placeholder there matches a frozen effect.

// The two halves of the restart story, said per field the way the settings form does. A
// per-project override is read from the configuration when a run is prepared; the
// process-wide values it falls back to are built into an options instance once at startup
// (2026-09-18-0f27), which is why changing THOSE is the half that needs a restart.
// Their words are the dictionary's config.sandbox.nextRun and config.sandbox.nextTurn.

// The hold window's half of the same story, and it is the opposite one: the next design
// conversation TURN takes it, and a reaper scan picks it up within its own interval — no
// restart, no new run. Zero is a value, not an empty box: it holds nothing.

/** An empty control means "inherit", so it is sent as undefined — never 0, never "". */
const text = (v: string) => (v.trim() === "" ? undefined : v);

/** What the inherited value reads as beside a control. A run-resolved value has no
 *  config-time answer at all, and a blank there would read as "no image". */
function provenance(t: T, v: ResolvedValue<string | number> | undefined): string {
  if (!v) return t("config.sandbox.provenance.unavailable");
  if (v.source === "run-resolved") return t("config.sandbox.provenance.runResolved");
  if (v.value === null || v.value === "") return t("config.sandbox.provenance.nothing");
  // 2026-09-23-2446: the legs that are NOT a settings field say so, or an operator goes
  // looking for a value in Settings that is not there.
  if (v.source === "environment-variable") return t("config.sandbox.provenance.environment", { value: v.value });
  if (v.source === "code-default") return t("config.sandbox.provenance.builtIn", { value: v.value });
  return t("config.sandbox.provenance.inherits", { value: v.value });
}

function placeholder(v: ResolvedValue<string | number> | undefined): string | undefined {
  if (!v || v.source === "run-resolved" || v.value === null || v.value === "") return undefined;
  return String(v.value);
}

export function ProjectSandboxSection({
  project,
  onChange,
  inheritedSandbox,
}: {
  project: StudioProject;
  onChange: (next: StudioProject) => void;
  inheritedSandbox: InheritedSandboxProjection | null;
}) {
  const t = useT();
  // A project being created — or renamed before its save — is not in the running
  // configuration and has no row of its own. The process-wide values are exactly what it
  // will inherit the moment it exists, so they are shown and SAID, rather than left blank.
  const row: InheritedSandbox | undefined =
    inheritedSandbox?.projects[project.id] ?? inheritedSandbox?.processWide;
  const draft: boolean =
    inheritedSandbox !== null && inheritedSandbox.projects[project.id] === undefined;

  // The form always sends the WHOLE block once it is shown: within a sent block a null
  // field means cleared, which is the only discriminator that tells a deliberate clear
  // from a client that never knew the field.
  const block: ProjectSandbox = project.sandbox ?? {};
  const set = (patch: ProjectSandbox) =>
    onChange({ ...project, sandbox: { ...block, ...patch } });

  // The structured three carry the same rule one level down: this section renders them, so
  // every save it provokes sends all three, and an undefined field inside the sent block is
  // a deliberate clear. A client that never draws them sends no structured block at all and
  // the stored resources, image pins and secret references are left alone.
  const structured: ProjectSandboxStructured = block.structured ?? {};
  const setStructured = (patch: ProjectSandboxStructured) =>
    set({ structured: { ...structured, ...patch } });

  return (
    <>
      <p className="help" data-testid="form-sandbox-inheritance-note">
        {t("config.sandbox.inheritanceNote")}
      </p>

      {inheritedSandbox === null && (
        <p className="help" data-testid="form-sandbox-inherited-unavailable">
          {t("config.sandbox.inheritedUnavailable")}
        </p>
      )}

      {draft && (
        <p className="help" data-testid="form-sandbox-draft-note">
          {t("config.sandbox.draftNote")}
        </p>
      )}

      <TextField
        label={t("config.sandbox.toolchainImage.label")}
        value={block.toolchainImage ?? ""}
        mono
        placeholder={placeholder(row?.toolchainImage)}
        help={t("config.sandbox.toolchainImage.help", { provenance: provenance(t, row?.toolchainImage) })}
        testId="form-field-sandbox-toolchainImage"
        onChange={(v) => set({ toolchainImage: text(v) })}
      />
      <NumberField
        label={t("config.sandbox.stepTimeout.label")}
        value={block.stepTimeoutSeconds ?? undefined}
        placeholder={placeholder(row?.stepTimeoutSeconds)}
        help={t("config.sandbox.stepTimeout.help", { provenance: provenance(t, row?.stepTimeoutSeconds) })}
        testId="form-field-sandbox-stepTimeoutSeconds"
        onChange={(v) => set({ stepTimeoutSeconds: v })}
      />
      <NumberField
        label={t("config.sandbox.runCommandTimeout.label")}
        value={block.runCommandTimeoutSeconds ?? undefined}
        placeholder={placeholder(row?.runCommandTimeoutSeconds)}
        help={t("config.sandbox.runCommandTimeout.help", { provenance: provenance(t, row?.runCommandTimeoutSeconds) })}
        testId="form-field-sandbox-runCommandTimeoutSeconds"
        onChange={(v) => set({ runCommandTimeoutSeconds: v })}
      />
      <TextField
        label={t("config.sandbox.agentRegistry.label")}
        value={block.agentRegistry ?? ""}
        mono
        placeholder={placeholder(row?.agentRegistry)}
        help={t("config.sandbox.agentRegistry.help", { provenance: provenance(t, row?.agentRegistry) })}
        testId="form-field-sandbox-agentRegistry"
        onChange={(v) => set({ agentRegistry: text(v) })}
      />
      <TextField
        label={t("config.sandbox.agentVersion.label")}
        value={block.agentVersion ?? ""}
        mono
        placeholder={placeholder(row?.agentVersion)}
        help={t("config.sandbox.agentVersion.help", { provenance: provenance(t, row?.agentVersion) })}
        testId="form-field-sandbox-agentVersion"
        onChange={(v) => set({ agentVersion: text(v) })}
      />

      <NumberField
        label={t("config.sandbox.hold.label")}
        value={block.holdSeconds ?? undefined}
        placeholder={placeholder(row?.holdSeconds)}
        help={t("config.sandbox.hold.help", { provenance: provenance(t, row?.holdSeconds) })}
        testId="form-field-sandbox-holdSeconds"
        onChange={(v) => set({ holdSeconds: v })}
      />

      {/* 2026-10-01-283df: config decides the browser, because a run's capacity is reserved
          before it starts — the model can only use a pod admission already counted. */}
      <CheckField
        label={t("config.sandbox.browser.label")}
        value={block.browserEnabled === true}
        testId="form-field-sandbox-browserEnabled"
        onChange={(v) => set({ browserEnabled: v ? true : undefined })}
      />
      <p className="help" data-testid="form-sandbox-browser-note">
        {t("config.sandbox.browser.note")}
      </p>

      {/* 2026-10-25-c5a7a/ff3b: with installation hardening on, root is a per-project choice that warns on every run. */}
      <CheckField
        label={t("config.sandbox.root.label")}
        value={block.runAsRoot === true}
        testId="form-field-sandbox-runAsRoot"
        onChange={(v) => set({ runAsRoot: v ? true : undefined })}
      />
      <p className="help" data-testid="form-sandbox-root-note">
        {t("config.sandbox.root.note")}
      </p>

      <SandboxResourceGroup
        value={structured.resources}
        inherited={row?.resources}
        onChange={(resources) => setStructured({ resources: resources ?? null })}
      />

      <MapField
        label={t("config.sandbox.images.label")}
        values={structured.images ?? {}}
        testId="form-field-sandbox-images"
        help={t("config.sandbox.images.help")}
        rowHint={(key, value) => imageHint(t, row, key, value)}
        onChange={(images) => setStructured({ images: images ?? null })}
      />

      <SandboxSecretsBlock
        value={structured.secrets}
        onChange={(secrets) => setStructured({ secrets: secrets ?? null })}
      />
    </>
  );
}

/** What a row of the image map inherits, PER KEY: the merge is per key, so pinning one
 *  language never replaces the table — every other key still answers from the code
 *  defaults. A key the product does not know inherits nothing nameable; the image is then
 *  chosen per run from the repository. */
function imageHint(t: T, row: InheritedSandbox | undefined, key: string, value: string) {
  const inherited = row?.images?.[key.trim().toLowerCase()]?.value ?? undefined;
  if (key.trim() === "") return {};
  if (value.trim() !== "")
    return {
      placeholder: inherited ?? undefined,
      note: inherited
        ? t("config.sandbox.images.overrides", { image: inherited, key })
        : t("config.sandbox.images.pins", { key }),
    };
  return {
    placeholder: inherited ?? undefined,
    note: inherited ? t("config.sandbox.provenance.inherits", { value: inherited }) : t("config.sandbox.images.noDefault"),
  };
}
