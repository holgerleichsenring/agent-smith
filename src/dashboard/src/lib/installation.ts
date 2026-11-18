// 2026-11-17-f3a8a: the installation's own conversation — the admin assistant — carries this label where a project
// name goes. The server offers it to admins only; the page shows it under the name people know it by.
export const INSTALLATION = "@agent-smith";

export function isInstallation(name: string | null | undefined): boolean {
  return name === INSTALLATION;
}

/** The name a project is shown under: its own, or "Agent Smith" for the installation. */
export function projectLabel(name: string): string {
  return isInstallation(name) ? "Agent Smith" : name;
}

/** The projects a default pick may land on — every one but the installation. */
export function pickable<T extends { name: string } | string>(projects: T[]): T[] {
  return projects.filter((p) => !isInstallation(typeof p === "string" ? p : p.name));
}
