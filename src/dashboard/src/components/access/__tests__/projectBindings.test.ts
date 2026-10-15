import { describe, it, expect } from "vitest";
import type { AccessDocument, AccessView, ProjectBinding } from "@/lib/accessApi";
import {
  addBinding,
  effectivePermissions,
  projectRoles,
  projectsOf,
  removeBinding,
  setBindingGroups,
} from "../projectBindings";

// 2026-10-15-4a4df: the Projects pane's draft operations — pure over (view, draft).

function binding(project: string, overrides: Partial<ProjectBinding> = {}): ProjectBinding {
  return { project, groups: [], people: [], roles: ["operator"], ...overrides };
}

function draft(projectBindings: ProjectBinding[]): AccessDocument {
  return {
    roleClaim: "roles",
    groupClaim: "groups",
    groupRoles: {},
    roles: {},
    personGrants: [],
    projectBindings,
    observationRetentionDays: 90,
  };
}

const view = {
  projects: ["billing", "web"],
  projectScopable: ["runs.read", "runs.control"],
  roles: [
    { name: "operator", builtIn: true, permissions: ["runs.control", "runs.read"], people: 0, groups: 0 },
    { name: "admin", builtIn: true, permissions: ["config.write", "runs.read"], people: 0, groups: 0 },
    { name: "auditor", builtIn: false, permissions: ["config.read"], people: 0, groups: 0 },
  ],
} as unknown as AccessView;

describe("projectBindings", () => {
  it("projectsOf_UnionsConfiguredAndBoundProjects_MarksTheUnconfigured", () => {
    const rows = projectsOf(view, draft([binding("web"), binding("retired")]));

    expect(rows.map((row) => [row.name, row.configured, row.bindings.length])).toEqual([
      ["billing", true, 0],
      ["web", true, 1],
      ["retired", false, 1],
    ]);
  });

  it("projectsOf_BindingDifferingOnlyInCase_SitsUnderTheConfiguredRow", () => {
    const rows = projectsOf(view, draft([binding("Billing")]));

    expect(rows).toHaveLength(2);
    expect(rows[0]).toMatchObject({ name: "billing", configured: true });
    expect(rows[0].bindings).toHaveLength(1);
  });

  it("Bindings_AddressedByProjectAndIndex_ChangeOnlyTheOneAddressed", () => {
    let doc = addBinding(draft([binding("web")]), "web");
    doc = setBindingGroups(doc, "WEB", 1, ["web-team"]);

    expect(doc.projectBindings.map((b) => b.groups)).toEqual([[], ["web-team"]]);
    expect(removeBinding(doc, "web", 0).projectBindings).toEqual([binding("web", { roles: [], groups: ["web-team"] })]);
  });

  it("effectivePermissions_SplitsScopedFromIgnored", () => {
    expect(effectivePermissions(view, ["admin", "operator"])).toEqual({
      scoped: ["runs.control", "runs.read"],
      ignored: ["config.write"],
    });
  });

  it("projectRoles_OffersOnlyRolesCarryingAScopablePermission", () => {
    expect(projectRoles(view)).toEqual(["operator", "admin"]);
  });
});
