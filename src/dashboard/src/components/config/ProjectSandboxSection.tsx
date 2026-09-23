"use client";

import type {
  InheritedSandbox,
  InheritedSandboxProjection,
  ProjectSandbox,
  ProjectSandboxStructured,
  ResolvedValue,
  StudioProject,
} from "@/lib/configApi";
import { MapField, NumberField, TextField } from "./formFields";
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
const NEXT_RUN = "applies to the next run of this project";

// The hold window's half of the same story, and it is the opposite one: the next design
// conversation TURN takes it, and a reaper scan picks it up within its own interval — no
// restart, no new run. Zero is a value, not an empty box: it holds nothing.
const NEXT_TURN =
  "applies to the next turn of a design conversation and reaches a reaper scan "
  + "without a server restart; 0 holds nothing";

/** An empty control means "inherit", so it is sent as undefined — never 0, never "". */
const text = (v: string) => (v.trim() === "" ? undefined : v);

/** What the inherited value reads as beside a control. A run-resolved value has no
 *  config-time answer at all, and a blank there would read as "no image". */
function provenance(v: ResolvedValue<string | number> | undefined): string {
  if (!v) return "inherited value unavailable";
  if (v.source === "run-resolved") return "detected per run from the repository";
  if (v.value === null || v.value === "") return "nothing to inherit — none is configured";
  // 2026-09-23-2446: the legs that are NOT a settings field say so, or an operator goes
  // looking for a value in Settings that is not there.
  if (v.source === "environment-variable")
    return `inherits ${v.value} from the SANDBOX_HOLD_SECONDS environment variable`;
  if (v.source === "code-default") return `inherits the built-in ${v.value}`;
  return `inherits ${v.value}`;
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
        Every field here is an override. Leave one empty and the project inherits what the
        placeholder names; {NEXT_RUN}. The process-wide values it falls back to — the hold
        window excepted, which is read live — are read once at startup, so changing THOSE in
        Settings → sandbox needs a server restart.
      </p>

      {inheritedSandbox === null && (
        <p className="help" data-testid="form-sandbox-inherited-unavailable">
          The inherited values could not be read, so the placeholders are missing. An empty
          field still means “inherit”.
        </p>
      )}

      {draft && (
        <p className="help" data-testid="form-sandbox-draft-note">
          This project is not in the running configuration yet, so it has no resolved row —
          the values named below are the process-wide ones it will inherit once it is saved.
        </p>
      )}

      <TextField
        label="toolchain image"
        value={block.toolchainImage ?? ""}
        mono
        placeholder={placeholder(row?.toolchainImage)}
        help={`whole-project sandbox image — ${provenance(row?.toolchainImage)}`}
        testId="form-field-sandbox-toolchainImage"
        onChange={(v) => set({ toolchainImage: text(v) })}
      />
      <NumberField
        label="step timeout (seconds)"
        value={block.stepTimeoutSeconds ?? undefined}
        placeholder={placeholder(row?.stepTimeoutSeconds)}
        help={`per-step wall-time cap for this project — ${provenance(row?.stepTimeoutSeconds)}`}
        testId="form-field-sandbox-stepTimeoutSeconds"
        onChange={(v) => set({ stepTimeoutSeconds: v })}
      />
      <NumberField
        label="run_command timeout (seconds)"
        value={block.runCommandTimeoutSeconds ?? undefined}
        placeholder={placeholder(row?.runCommandTimeoutSeconds)}
        help={`what a run_command gets when it asks for none — ${provenance(row?.runCommandTimeoutSeconds)}`}
        testId="form-field-sandbox-runCommandTimeoutSeconds"
        onChange={(v) => set({ runCommandTimeoutSeconds: v })}
      />
      <TextField
        label="agent registry"
        value={block.agentRegistry ?? ""}
        mono
        placeholder={placeholder(row?.agentRegistry)}
        help={`registry this project pulls the sandbox agent from — ${provenance(row?.agentRegistry)}`}
        testId="form-field-sandbox-agentRegistry"
        onChange={(v) => set({ agentRegistry: text(v) })}
      />
      <TextField
        label="agent version"
        value={block.agentVersion ?? ""}
        mono
        placeholder={placeholder(row?.agentVersion)}
        help={`sandbox agent image tag for this project — ${provenance(row?.agentVersion)}`}
        testId="form-field-sandbox-agentVersion"
        onChange={(v) => set({ agentVersion: text(v) })}
      />

      <NumberField
        label="sandbox hold (seconds)"
        value={block.holdSeconds ?? undefined}
        placeholder={placeholder(row?.holdSeconds)}
        help={`how long this project's design conversations hold their source sandboxes between turns — ${provenance(row?.holdSeconds)}; ${NEXT_TURN}`}
        testId="form-field-sandbox-holdSeconds"
        onChange={(v) => set({ holdSeconds: v })}
      />

      <SandboxResourceGroup
        value={structured.resources}
        inherited={row?.resources}
        onChange={(resources) => setStructured({ resources: resources ?? null })}
      />

      <MapField
        label="per-language images"
        values={structured.images ?? {}}
        testId="form-field-sandbox-images"
        help="one language and the image it is built in — every language you do not name keeps inheriting"
        rowHint={(key, value) => imageHint(row, key, value)}
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
function imageHint(row: InheritedSandbox | undefined, key: string, value: string) {
  const inherited = row?.images?.[key.trim().toLowerCase()]?.value ?? undefined;
  if (key.trim() === "") return {};
  if (value.trim() !== "")
    return {
      placeholder: inherited ?? undefined,
      note: inherited
        ? `overrides the code default ${inherited} for ${key} only`
        : `pins ${key}, which the product has no code default for`,
    };
  return {
    placeholder: inherited ?? undefined,
    note: inherited ? `inherits ${inherited}` : "no code default for this key",
  };
}
