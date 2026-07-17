import type { ConfigProject } from "@/lib/configApi";

// p0271: the project picker that replaced the topology graph. A plain labelled
// combobox — pick a project, the detail sheet below shows everything about it.
// p0343d: parity re-dress — the mock's uppercase field label + panel select.

export function ProjectSelect({
  projects,
  selected,
  onSelect,
}: {
  projects: ConfigProject[];
  selected: string;
  onSelect: (name: string) => void;
}) {
  return (
    <label className="pick-label">
      <span className="k">Project</span>
      <select
        data-testid="project-select"
        value={selected}
        onChange={(e) => onSelect(e.target.value)}
      >
        {projects.map((p) => (
          <option key={p.name} value={p.name}>
            {p.name}
          </option>
        ))}
      </select>
    </label>
  );
}
