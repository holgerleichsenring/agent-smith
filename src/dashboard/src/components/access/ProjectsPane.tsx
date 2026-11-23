"use client";

import type { AccessDocument, AccessView } from "@/lib/accessApi";
import { BindingCard } from "./BindingCard";
import { peopleOf } from "./derive";
import { PagedRows } from "./PagedRows";
import {
  addBinding,
  projectsOf,
  removeBinding,
  setBindingGroups,
  setBindingPeople,
  setBindingRoles,
} from "./projectBindings";
import { useT } from "@/hooks/useT";

// 2026-10-15-4a4df: Access → Projects. Roles held in ONE project, beside the global grants the
// other panes edit. A project may carry several bindings — "team A operates, team B reads" is
// two — and a binding naming a project no longer configured stays listed, marked, because it
// applies again the moment the project is.

export function ProjectsPane({
  view,
  draft,
  onChange,
}: {
  view: AccessView;
  draft: AccessDocument;
  onChange: (next: AccessDocument) => void;
}) {
  const t = useT();
  const rows = projectsOf(view, draft);
  const people = peopleOf(view, draft)
    .filter((person) => person.nameClaim === view.nameClaim)
    .map((person) => person.nameValue);

  return (
    <PagedRows
      rows={rows}
      testId="access-projects"
      searchLabel={t("access.projects.search")}
      searchPlaceholder={t("access.projects.searchPlaceholder")}
      matchesQuery={(row, query) => row.name.toLowerCase().includes(query)}
      filters={[
        { key: "all", label: t("access.filter.all"), matches: () => true },
        { key: "bound", label: t("access.projects.bound"), matches: (row) => row.bindings.length > 0 },
        { key: "unbound", label: t("access.projects.unbound"), matches: (row) => row.bindings.length === 0 },
        { key: "unconfigured", label: t("access.projects.notConfiguredFilter"), matches: (row) => !row.configured },
      ]}
      emptyText={t("access.projects.empty")}
      header={
        <tr>
          <th>{t("access.projects.project")}</th>
          <th>{t("access.projects.bindings")}</th>
        </tr>
      }
      renderRow={(row) => (
        <tr key={row.name} data-testid={`access-project-${row.name}`}>
          <td className="project-cell">
            <div className="who-name">{row.name}</div>
            {!row.configured && (
              <span className="unconfigured" data-testid={`access-project-${row.name}-unconfigured`}>
                {t("access.projects.notConfigured")}
              </span>
            )}
          </td>
          <td>
            <div className="bindings">
              {row.bindings.map((binding, index) => (
                <BindingCard
                  key={index}
                  testId={`access-binding-${row.name}-${index}`}
                  view={view}
                  binding={binding}
                  people={people}
                  onGroups={(groups) => onChange(setBindingGroups(draft, row.name, index, groups))}
                  onPeople={(refs) => onChange(setBindingPeople(draft, row.name, index, refs))}
                  onRoles={(roles) => onChange(setBindingRoles(draft, row.name, index, roles))}
                  onRemove={() => onChange(removeBinding(draft, row.name, index))}
                />
              ))}
              <button
                type="button"
                className="more-btn"
                data-testid={`access-project-${row.name}-add`}
                onClick={() => onChange(addBinding(draft, row.name))}
              >
                {t("access.projects.addBinding")}
              </button>
            </div>
          </td>
        </tr>
      )}
    />
  );
}
