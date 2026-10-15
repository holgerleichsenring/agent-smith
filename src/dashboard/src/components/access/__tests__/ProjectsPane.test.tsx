import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import type { AccessDocument, AccessView, ProjectBinding } from "@/lib/accessApi";
import { AccessStudio } from "../AccessStudio";

// 2026-10-15-4a4df: Access → Projects — groups and people bound to roles in one project,
// saved with the WHOLE access document.

const saveAccess = vi.fn((document: AccessDocument) => Promise.resolve(viewWith(document)));
const fetchAccess = vi.fn(() => Promise.resolve(viewWith(document())));

vi.mock("@/lib/accessApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/accessApi")>()),
  fetchAccess: () => fetchAccess(),
  saveAccess: (document: AccessDocument) => saveAccess(document),
  forgetPerson: () => Promise.resolve(viewWith(document())),
}));

beforeEach(() => {
  vi.clearAllMocks();
});

const NAME_CLAIM = "preferred_username";

function document(projectBindings: ProjectBinding[] = []): AccessDocument {
  return {
    roleClaim: "app_roles",
    groupClaim: "memberOf",
    groupRoles: { "platform-admins": ["admin"] },
    roles: { auditor: ["config.read"] },
    personGrants: [{ claim: NAME_CLAIM, value: "ada@example.com", roles: ["admin"] }],
    projectBindings,
    observationRetentionDays: 90,
  };
}

function viewWith(doc: AccessDocument): AccessView {
  return {
    roleClaim: doc.roleClaim,
    groupClaim: doc.groupClaim,
    nameClaim: NAME_CLAIM,
    document: doc,
    nameClaimIsSelfAsserted: false,
    observationRetentionDays: 90,
    people: [],
    groups: [{ value: "billing-team", roles: [], carriers: 2 }],
    roles: [
      { name: "admin", builtIn: true, permissions: ["config.write", "runs.control", "runs.read"], people: 0, groups: 1 },
      { name: "auditor", builtIn: false, permissions: ["config.read"], people: 0, groups: 0 },
      { name: "operator", builtIn: true, permissions: ["runs.control", "runs.read"], people: 0, groups: 0 },
    ],
    permissions: ["config.read", "config.write", "runs.control", "runs.read"],
    findings: [],
    projects: ["billing", "web"],
    projectScopable: ["runs.control", "runs.read"],
  };
}

function binding(project: string, roles: string[] = ["operator"]): ProjectBinding {
  return { project, groups: [], people: [], roles };
}

async function openProjects(doc: AccessDocument = document()) {
  fetchAccess.mockResolvedValueOnce(viewWith(doc));
  render(<AccessStudio />);
  await screen.findByTestId("access-studio");
  fireEvent.click(screen.getByTestId("access-tab-projects"));
  return screen.findByTestId("access-projects-rows");
}

describe("ProjectsPane", () => {
  it("ProjectsPane_AddBindingWithGroupPersonAndRole_SavesTheWholeDocumentWithIt", async () => {
    await openProjects();
    fireEvent.click(screen.getByTestId("access-project-billing-add"));
    fireEvent.change(screen.getByTestId("access-binding-billing-0-groups-pick"), { target: { value: "billing-team" } });
    fireEvent.change(screen.getByTestId("access-binding-billing-0-people-value"), { target: { value: "grace" } });
    fireEvent.click(screen.getByTestId("access-binding-billing-0-people-add"));
    fireEvent.change(screen.getByTestId("access-binding-billing-0-roles-grant"), { target: { value: "operator" } });
    fireEvent.click(screen.getByTestId("access-save"));

    await waitFor(() => expect(saveAccess).toHaveBeenCalled());
    const sent = saveAccess.mock.calls[0][0];
    expect(sent.projectBindings).toEqual([
      { project: "billing", groups: ["billing-team"], people: [{ claim: NAME_CLAIM, value: "grace" }], roles: ["operator"] },
    ]);
    expect(sent.roleClaim).toBe("app_roles");
    expect(sent.groupClaim).toBe("memberOf");
    expect(sent.roles).toEqual({ auditor: ["config.read"] });
    expect(sent.personGrants).toEqual(document().personGrants);
  });

  it("ProjectsPane_RoleWithoutScopablePermissions_IsNotOffered", async () => {
    await openProjects(document([binding("billing", [])]));
    const offered = within(screen.getByTestId("access-binding-billing-0-roles-grant"))
      .getAllByRole("option").map((option) => option.textContent);

    expect(offered).toEqual(["+ grant", "admin", "operator"]);
  });

  it("ProjectsPane_RoleCarryingGlobalOnlyPermissions_ShowsThemAsIgnoredHere", async () => {
    await openProjects(document([binding("billing", ["admin"])]));

    expect(screen.getByTestId("access-binding-billing-0-effective")).toHaveTextContent("runs.control, runs.read");
    expect(screen.getByTestId("access-binding-billing-0-ignored")).toHaveTextContent("ignored here: config.write");
  });

  it("ProjectsPane_TwoBindingsInOneProject_EditIndependently", async () => {
    await openProjects(document([binding("web"), binding("web", ["admin"])]));
    fireEvent.click(screen.getByTestId("access-binding-web-1-roles-withdraw-admin"));
    fireEvent.click(screen.getByTestId("access-save"));

    await waitFor(() => expect(saveAccess).toHaveBeenCalled());
    expect(saveAccess.mock.calls[0][0].projectBindings.map((b) => b.roles)).toEqual([["operator"], []]);
  });

  it("ProjectsPane_BindingForUnconfiguredProject_ShowsNotConfigured", async () => {
    await openProjects(document([binding("retired")]));

    expect(screen.getByTestId("access-project-retired-unconfigured")).toHaveTextContent("not configured");
    expect(screen.queryByTestId("access-project-billing-unconfigured")).toBeNull();
  });

  it("ProjectsPane_RemoveBinding_DropsItFromTheDraftOnly", async () => {
    await openProjects(document([binding("web")]));
    fireEvent.click(screen.getByTestId("access-binding-web-0-remove"));

    expect(screen.queryByTestId("access-binding-web-0")).toBeNull();
    expect(saveAccess).not.toHaveBeenCalled();
    expect(screen.getByTestId("access-save")).toBeEnabled();
  });

  it("AccessStudio_ProjectsTab_CountsBoundProjects", async () => {
    fetchAccess.mockResolvedValueOnce(viewWith(document([binding("web"), binding("web"), binding("retired")])));
    render(<AccessStudio />);

    expect(await screen.findByTestId("access-tab-projects")).toHaveTextContent("Projects2");
  });
});
