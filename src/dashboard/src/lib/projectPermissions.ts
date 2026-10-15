import type { CallerIdentity } from "@/lib/identityApi";

// 2026-10-15-4a4de: what a caller holds in a project — its global permissions plus that project's binding grants,
// the project matched case-insensitively as the server's ConfigNames.Comparer does. No project is the global rule.

function grantsIn(identity: CallerIdentity, project: string): string[] {
  const wanted = project.toLowerCase();
  return Object.entries(identity.projectPermissions ?? {})
    .filter(([name]) => name.toLowerCase() === wanted)
    .flatMap(([, granted]) => granted);
}

export function holds(identity: CallerIdentity, permission: string, project?: string): boolean {
  return identity.permissions.includes(permission)
    || (project !== undefined && grantsIn(identity, project).includes(permission));
}

/** Held globally or in at least one project — for a route that names no project (the bulk clear). */
export function holdsInAnyProject(identity: CallerIdentity, permission: string): boolean {
  return identity.permissions.includes(permission)
    || Object.values(identity.projectPermissions ?? {}).some((granted) => granted.includes(permission));
}
