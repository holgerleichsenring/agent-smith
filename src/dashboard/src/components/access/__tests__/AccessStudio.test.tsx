import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import type { AccessDocument, AccessPerson, AccessView } from "@/lib/accessApi";
import { AccessStudio } from "../AccessStudio";

// 2026-08-26-7a51: the access surface — four panes over one document, at directory scale.
//
// The save sends the WHOLE document: the server binds a settings body onto a fresh model,
// so a people-only body would reset the claim names and delete the custom roles. That is
// the one thing these tests are most careful about.

const saveAccess = vi.fn((document: AccessDocument) => Promise.resolve(viewWith(document)));
const forgetPerson = vi.fn(() => Promise.resolve(view()));
const fetchAccess = vi.fn(() => Promise.resolve(view()));

vi.mock("@/lib/accessApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/accessApi")>()),
  fetchAccess: () => fetchAccess(),
  saveAccess: (document: AccessDocument) => saveAccess(document),
  forgetPerson: (id: string) => forgetPerson(id),
}));

beforeEach(() => {
  vi.clearAllMocks();
});

const NAME_CLAIM = "preferred_username";

function document(overrides: Partial<AccessDocument> = {}): AccessDocument {
  return {
    roleClaim: "app_roles",
    groupClaim: "memberOf",
    groupRoles: { "platform-admins": ["admin"] },
    roles: { auditor: ["config.read"] },
    personGrants: [{ claim: NAME_CLAIM, value: "ada@example.com", roles: ["admin"] }],
    observationRetentionDays: 90,
    ...overrides,
  };
}

function person(index: number): AccessPerson {
  return {
    id: `subject-${index}`,
    subject: `subject-${index}`,
    nameClaim: NAME_CLAIM,
    nameValue: `person${index}@example.com`,
    directoryRoles: index === 1 ? [{ role: "operator", via: "memberOf" }] : [],
    grantedRoles: [],
    groupValues: [],
    groupsOmitted: false,
    firstSeen: "2026-08-01T09:00:00+00:00",
    lastSeen: "2026-08-26T09:00:00+00:00",
  };
}

function viewWith(doc: AccessDocument, overrides: Partial<AccessView> = {}): AccessView {
  const people = Array.from({ length: 40 }, (_, i) => person(i));
  return {
    roleClaim: doc.roleClaim,
    groupClaim: doc.groupClaim,
    nameClaim: NAME_CLAIM,
    document: doc,
    nameClaimIsSelfAsserted: true,
    observationRetentionDays: doc.observationRetentionDays,
    people: [
      { ...person(0), nameValue: "ada@example.com", id: "ada-0001", subject: "ada-0001", grantedRoles: ["admin"] },
      ...people.slice(1),
    ],
    groups: [{ value: "platform-admins", roles: ["admin"], carriers: 3 }],
    roles: [
      { name: "admin", builtIn: true, permissions: ["config.read", "config.write", "access.write"], people: 1, groups: 1 },
      { name: "auditor", builtIn: false, permissions: ["config.read"], people: 0, groups: 0 },
      { name: "reader", builtIn: true, permissions: ["config.read"], people: 0, groups: 0 },
    ],
    permissions: ["access.write", "config.read", "config.write"],
    findings: [],
    ...overrides,
  };
}

function view(overrides: Partial<AccessView> = {}): AccessView {
  return viewWith(document(), overrides);
}

describe("AccessStudio", () => {
  it("Surface_ManyPeople_PagesAndSearches", async () => {
    render(<AccessStudio />);
    await screen.findByTestId("access-studio");

    // Twelve rows of forty, and the tally says so rather than leaving the reader to count.
    await waitFor(() =>
      expect(screen.getByTestId("access-people-tally")).toHaveTextContent("Showing 12 of 40"),
    );
    expect(within(screen.getByTestId("access-people-rows")).getAllByRole("row")).toHaveLength(12);

    fireEvent.click(screen.getByTestId("access-people-more"));
    expect(screen.getByTestId("access-people-tally")).toHaveTextContent("Showing 37 of 40");

    fireEvent.change(screen.getByTestId("access-people-search"), {
      target: { value: "person12@" },
    });
    expect(screen.getByTestId("access-people-tally")).toHaveTextContent("Showing 1 of 1");
    expect(screen.getByTestId("access-person-person12@example.com")).toBeInTheDocument();
  });

  // 2026-09-14-91ad: a role composed here, instead of a refusal and a config import.
  it("RolesPane_CustomRoleWithAPermissionTheCatalogDropped_EditsTheStoredBundleNotTheRenderedOne", async () => {
    // The stored bundle names a permission the server's catalog did not recognise, so the
    // MATRIX renders one permission and the document holds two. An editor reading the
    // matrix would silently drop the second the first time anybody touched the first.
    const stored = document({ roles: { auditor: ["config.read", "runs.invent"] } });
    fetchAccess.mockResolvedValueOnce(viewWith(stored));

    render(<AccessStudio />);
    await screen.findByTestId("access-studio");
    fireEvent.click(screen.getByTestId("access-tab-roles"));

    fireEvent.click(await screen.findByTestId("access-role-auditor-config.write"));
    fireEvent.click(screen.getByTestId("access-save"));

    await waitFor(() => expect(saveAccess).toHaveBeenCalled());
    expect(saveAccess.mock.calls[0][0].roles.auditor).toEqual([
      "config.read",
      "runs.invent",
      "config.write",
    ]);
  });

  it("RolesPane_BuiltInRole_RendersWithoutAnEditAffordance", async () => {
    render(<AccessStudio />);
    await screen.findByTestId("access-studio");
    fireEvent.click(screen.getByTestId("access-tab-roles"));

    // The built-in bundles are not in the document at all, which is what makes a custom
    // role additive rather than a redefinition.
    expect(await screen.findByTestId("access-role-edit-auditor")).toBeInTheDocument();
    expect(screen.queryByTestId("access-role-edit-admin")).toBeNull();
    expect(screen.queryByTestId("access-role-remove-reader")).toBeNull();
  });

  it("RolesPane_ANewRole_IsAddedToTheDocumentWithItsPickedPermissions", async () => {
    render(<AccessStudio />);
    await screen.findByTestId("access-studio");
    fireEvent.click(screen.getByTestId("access-tab-roles"));

    fireEvent.change(await screen.findByTestId("access-role-new-name"), {
      target: { value: "release-manager" },
    });
    fireEvent.click(screen.getByTestId("access-role-add"));
    fireEvent.click(screen.getByTestId("access-role-release-manager-config.read"));
    fireEvent.click(screen.getByTestId("access-save"));

    await waitFor(() => expect(saveAccess).toHaveBeenCalled());
    expect(saveAccess.mock.calls[0][0].roles["release-manager"]).toEqual(["config.read"]);
  });

  it("Surface_NameClaimIsNotSub_WarnsAboutSelfAssertedValues", async () => {
    render(<AccessStudio />);

    const warning = await screen.findByTestId("access-nameclaim-warning");
    expect(warning).toHaveTextContent(NAME_CLAIM);
    expect(warning).toHaveTextContent("self-asserted");
  });

  it("NameClaimIsSub_SaysNothing", async () => {
    fetchAccess.mockResolvedValueOnce(view({ nameClaim: "sub", nameClaimIsSelfAsserted: false }));
    render(<AccessStudio />);
    await screen.findByTestId("access-studio");

    expect(screen.queryByTestId("access-nameclaim-warning")).not.toBeInTheDocument();
  });

  it("Save_FromThePeoplePane_LeavesTheClaimNamesAsTheyWere", async () => {
    render(<AccessStudio />);
    await screen.findByTestId("access-studio");

    fireEvent.change(screen.getByTestId("access-person-person2@example.com-roles-grant"), {
      target: { value: "reader" },
    });
    fireEvent.click(screen.getByTestId("access-save"));

    await waitFor(() => expect(saveAccess).toHaveBeenCalledTimes(1));
    const sent = saveAccess.mock.calls[0][0];
    expect(sent.roleClaim).toBe("app_roles");
    expect(sent.groupClaim).toBe("memberOf");
    expect(sent.roles).toEqual({ auditor: ["config.read"] });
    expect(sent.observationRetentionDays).toBe(90);
    expect(sent.personGrants).toContainEqual({
      claim: NAME_CLAIM,
      value: "person2@example.com",
      roles: ["reader"],
    });
  });

  it("Chips_CarryWhereTheRoleCameFrom", async () => {
    render(<AccessStudio />);
    await screen.findByTestId("access-studio");

    const directory = await screen.findByTestId(
      "access-person-person1@example.com-roles-directory-operator",
    );
    expect(directory).toHaveClass("chip-directory");
    expect(screen.getByTestId("access-person-ada@example.com-roles-granted-admin")).toHaveClass(
      "chip-here",
    );
    // A role the directory sends cannot be taken back here; one granted here can.
    expect(
      screen.queryByTestId("access-person-person1@example.com-roles-withdraw-operator"),
    ).not.toBeInTheDocument();
  });

  it("Person_AddedByHand_ReadsNotSignedInYet", async () => {
    render(<AccessStudio />);
    await screen.findByTestId("access-studio");

    fireEvent.change(screen.getByTestId("access-people-add-value"), {
      target: { value: "newcomer@example.com" },
    });
    fireEvent.click(screen.getByTestId("access-people-add"));

    const row = await screen.findByTestId("access-person-newcomer@example.com");
    expect(row).toHaveTextContent("not signed in yet");
  });

  it("RemovePerson_RemovesTheGrantAndTheRecordTogether", async () => {
    render(<AccessStudio />);
    await screen.findByTestId("access-studio");

    fireEvent.click(screen.getByTestId("access-person-ada@example.com-forget"));

    await waitFor(() => expect(forgetPerson).toHaveBeenCalledWith("ada-0001"));
    expect(saveAccess).not.toHaveBeenCalled();
  });

  it("CustomRole_AlreadyConfigured_IsRenderedReadOnly", async () => {
    render(<AccessStudio />);
    await screen.findByTestId("access-studio");
    fireEvent.click(screen.getByTestId("access-tab-roles"));

    const card = await screen.findByTestId("access-role-auditor");
    expect(within(card).getByTestId("access-role-auditor-custom")).toHaveTextContent("read-only");
    expect(within(card).queryByRole("button")).not.toBeInTheDocument();
    expect(within(card).queryByRole("textbox")).not.toBeInTheDocument();
  });

  it("Groups_EditTheMappingTheInstallationAlreadyHad", async () => {
    render(<AccessStudio />);
    await screen.findByTestId("access-studio");
    fireEvent.click(screen.getByTestId("access-tab-groups"));

    fireEvent.click(
      await screen.findByTestId("access-group-platform-admins-roles-withdraw-admin"),
    );
    fireEvent.click(screen.getByTestId("access-save"));

    await waitFor(() => expect(saveAccess).toHaveBeenCalledTimes(1));
    expect(saveAccess.mock.calls[0][0].groupRoles).toEqual({});
  });

  it("Claims_AreEditedHere_AndTravelWithEverySave", async () => {
    render(<AccessStudio />);
    await screen.findByTestId("access-studio");
    fireEvent.click(screen.getByTestId("access-tab-claims"));

    fireEvent.change(await screen.findByTestId("access-claims-roleclaim"), {
      target: { value: "roles" },
    });
    fireEvent.click(screen.getByTestId("access-save"));

    await waitFor(() => expect(saveAccess).toHaveBeenCalledTimes(1));
    expect(saveAccess.mock.calls[0][0].roleClaim).toBe("roles");
    expect(saveAccess.mock.calls[0][0].personGrants).toHaveLength(1);
  });
});
