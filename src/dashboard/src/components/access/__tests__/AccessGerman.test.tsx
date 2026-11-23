import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { resetLanguage } from "@/lib/i18n/language";
import type { AccessDocument, AccessView } from "@/lib/accessApi";
import { RolesPane } from "../RolesPane";
import { withValues } from "../withValues";

// 2026-11-23-5669e: Access in German — a role card's counts and the editor below it read from the dictionary; role and
// permission names are data and stay as configured.

const document: AccessDocument = {
  roleClaim: "roles",
  groupClaim: "groups",
  groupRoles: { "platform-admins": ["admin"] },
  roles: { auditor: ["config.read"] },
  personGrants: [],
  projectBindings: [],
  observationRetentionDays: 90,
};

const view = {
  roles: [{ name: "admin", builtIn: true, permissions: ["config.read", "config.write"], people: 0, groups: 1 }],
  permissions: ["config.read", "config.write", "access.write"],
} as unknown as AccessView;

describe("Access in German", () => {
  it("RolesPane_German_WordsTheCountsButNotTheRoles", () => {
    resetLanguage("de");
    render(
      <RolesPane
        view={view}
        draft={document}
        people={[]}
        groups={[{ value: "platform-admins", roles: ["admin"], carriers: 3 }]}
        onChange={() => {}}
      />,
    );
    expect(screen.getByTestId("access-role-admin")).toHaveTextContent(
      "2 von 3 Berechtigungengetragen von 0 Personen · 1 Gruppe",
    );
    expect(screen.getByText("Berechtigung")).toBeInTheDocument();
    expect(screen.getByText("Hier konfigurierte Rollen")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Rolle hinzufügen" })).toBeInTheDocument();
    expect(screen.getByLabelText("admin hat config.read")).toBeInTheDocument();
  });

  it("WithValues_APlaceholder_BecomesTheNodeAndTheRestStays", () => {
    render(<p data-testid="line">{withValues("{n} of {total} and {other}", { n: <b>2</b>, total: 3 })}</p>);
    expect(screen.getByTestId("line")).toHaveTextContent("2 of 3 and {other}");
    expect(screen.getByTestId("line").querySelector("b")).toHaveTextContent("2");
  });
});
