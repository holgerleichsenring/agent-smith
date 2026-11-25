"use client";

import type { StudioEntity, StudioProject } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import { MachineIdentityFields } from "./MachineIdentityFields";

// 2026-11-25-9992h: the project's machine: block on the project form. The block is written only once a control here is
// touched; clearing every field clears it. 2026-11-25-0873: the fields themselves are MachineIdentityFields, shared with
// pocs.machine.

export function ProjectMachineSection({ project, onChange, secrets }: {
  project: StudioProject;
  onChange: (next: StudioProject) => void;
  secrets: StudioEntity[];
}) {
  const t = useT();
  return (
    <>
      <p className="help">{t("config.machine.help")}</p>
      <MachineIdentityFields value={project.machine} secrets={secrets} testPrefix="form-field-machine"
        onChange={(machine) => onChange({ ...project, machine })} />
    </>
  );
}
