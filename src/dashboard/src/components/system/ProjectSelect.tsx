import type { ConfigProject } from "@/lib/configApi";

// p0271: the project picker that replaced the topology graph. A plain labelled
// combobox — pick a project, the detail sheet below shows everything about it.

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
    <label className="flex items-center gap-2 dsh-body text-stone-600">
      <span className="dsh-label text-stone-400">Project</span>
      <select
        data-testid="project-select"
        value={selected}
        onChange={(e) => onSelect(e.target.value)}
        className="rounded-md border border-stone-300 bg-white px-3 py-1.5 dsh-body text-stone-700"
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
