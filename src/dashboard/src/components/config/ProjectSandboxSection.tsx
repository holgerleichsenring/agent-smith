"use client";

import type {
  InheritedSandbox,
  InheritedSandboxProjection,
  ProjectSandbox,
  ResolvedValue,
  StudioProject,
} from "@/lib/configApi";
import { NumberField, TextField } from "./formFields";

// 2026-09-22-6968: the project form's sixth tab — the five SCALAR per-project sandbox
// overrides, none of which was editable anywhere in the product before this. Every one is
// null-means-inherit, so every control has to say what CLEARING it restores; the
// placeholder is the counterfactual value the server computes (what this project would get
// with its sandbox block empty), never its own current value.
//
// The structured three (cpu/memory, the per-language image map, the pod's secrets) are
// deliberately absent — each inherits by a rule this counterfactual cannot give, and they
// are 2026-09-22-6c46.

// The two halves of the restart story, said per field the way the settings form does. A
// per-project override is read from the configuration when a run is prepared; the
// process-wide values it falls back to are built into an options instance once at startup
// (2026-09-18-0f27), which is why changing THOSE is the half that needs a restart.
const NEXT_RUN = "applies to the next run of this project";

/** An empty control means "inherit", so it is sent as undefined — never 0, never "". */
const text = (v: string) => (v.trim() === "" ? undefined : v);

/** What the inherited value reads as beside a control. A run-resolved value has no
 *  config-time answer at all, and a blank there would read as "no image". */
function provenance(v: ResolvedValue<string | number> | undefined): string {
  if (!v) return "inherited value unavailable";
  if (v.source === "run-resolved") return "detected per run from the repository";
  if (v.value === null || v.value === "") return "nothing to inherit — none is configured";
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

  return (
    <>
      <p className="help" data-testid="form-sandbox-inheritance-note">
        Every field here is an override. Leave one empty and the project inherits what the
        placeholder names; {NEXT_RUN}. The process-wide values below are read once at
        startup, so changing THOSE in Settings → sandbox needs a server restart.
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
    </>
  );
}
