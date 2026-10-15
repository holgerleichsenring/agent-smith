import type { AccessDocument, AccessView, PersonRef, ProjectBinding } from "@/lib/accessApi";

// 2026-10-15-4a4df: the Projects pane's draft operations, pure over (view, draft).
//
// Project names compare case-INSENSITIVELY, exactly as the server matches a binding to a
// configured project — so a binding for 'Billing' sits under the configured 'billing' row
// rather than in a second "not configured" one. A binding is addressed by its project and
// its position among that project's bindings.

export interface ProjectRow {
  name: string;
  configured: boolean;
  bindings: ProjectBinding[];
}

/** Configured projects, then every project only a draft binding names. */
export function projectsOf(view: AccessView, draft: AccessDocument): ProjectRow[] {
  const bound = draft.projectBindings
    .map((binding) => binding.project)
    .filter((name) => !view.projects.some((configured) => same(configured, name)));
  const unconfigured = [...new Map(bound.map((name) => [name.toLowerCase(), name])).values()].sort();
  return [
    ...view.projects.map((name) => ({ name, configured: true, bindings: bindingsOf(draft, name) })),
    ...unconfigured.map((name) => ({ name, configured: false, bindings: bindingsOf(draft, name) })),
  ];
}

export function addBinding(draft: AccessDocument, project: string): AccessDocument {
  const added: ProjectBinding = { project, groups: [], people: [], roles: [] };
  return { ...draft, projectBindings: [...draft.projectBindings, added] };
}

export function removeBinding(draft: AccessDocument, project: string, index: number): AccessDocument {
  const at = positionOf(draft, project, index);
  return { ...draft, projectBindings: draft.projectBindings.filter((_, i) => i !== at) };
}

export function setBindingGroups(draft: AccessDocument, project: string, index: number, groups: string[]) {
  return update(draft, project, index, (binding) => ({ ...binding, groups }));
}

export function setBindingPeople(draft: AccessDocument, project: string, index: number, people: PersonRef[]) {
  return update(draft, project, index, (binding) => ({ ...binding, people }));
}

export function setBindingRoles(draft: AccessDocument, project: string, index: number, roles: string[]) {
  return update(draft, project, index, (binding) => ({ ...binding, roles }));
}

/** What the roles grant inside a project, and what of them stays global and is ignored there. */
export function effectivePermissions(view: AccessView, roles: string[]): { scoped: string[]; ignored: string[] } {
  const held = new Set(
    view.roles.filter((role) => roles.some((name) => same(name, role.name))).flatMap((role) => role.permissions),
  );
  const all = [...held].sort();
  return {
    scoped: all.filter((permission) => view.projectScopable.includes(permission)),
    ignored: all.filter((permission) => !view.projectScopable.includes(permission)),
  };
}

/** The roles that mean something in a project: those carrying a project-scopable permission. */
export function projectRoles(view: AccessView): string[] {
  return view.roles
    .filter((role) => role.permissions.some((permission) => view.projectScopable.includes(permission)))
    .map((role) => role.name);
}

function bindingsOf(draft: AccessDocument, project: string): ProjectBinding[] {
  return draft.projectBindings.filter((binding) => same(binding.project, project));
}

function positionOf(draft: AccessDocument, project: string, index: number): number {
  const positions = draft.projectBindings.flatMap((binding, i) => (same(binding.project, project) ? [i] : []));
  return positions[index] ?? -1;
}

function update(
  draft: AccessDocument,
  project: string,
  index: number,
  change: (binding: ProjectBinding) => ProjectBinding,
): AccessDocument {
  const at = positionOf(draft, project, index);
  return { ...draft, projectBindings: draft.projectBindings.map((b, i) => (i === at ? change(b) : b)) };
}

function same(left: string, right: string): boolean {
  return left.toLowerCase() === right.toLowerCase();
}
