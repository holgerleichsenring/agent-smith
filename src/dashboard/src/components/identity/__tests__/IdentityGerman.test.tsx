import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { resetLanguage } from "@/lib/i18n/language";
import type { CallerIdentity } from "@/lib/identityApi";
import { ClaimFacts } from "../ClaimFacts";

// 2026-11-23-5669e: Your identity in German — the headings and the empty claims read from the dictionary; the claim
// names and values are what the token carried and stay as they came.

const identity = {
  subject: "ada-0001",
  issuer: null,
  roleClaim: "roles",
  roleClaimValues: [],
  groupClaim: "groups",
  groupClaimValues: ["platform-admins"],
  roles: [],
  permissions: [],
  projectPermissions: {},
  findings: [],
} as unknown as CallerIdentity;

describe("Identity in German", () => {
  it("ClaimFacts_German_WordsTheFactsButNotTheClaims", () => {
    resetLanguage("de");
    render(<ClaimFacts identity={identity} />);
    expect(screen.getByText("Angemeldet als")).toBeInTheDocument();
    expect(screen.getByText("– das Token nannte keinen Issuer")).toBeInTheDocument();
    expect(screen.getByTestId("identity-role-claim")).toHaveTextContent("Rollen-Claim · rolesnichts angekommen");
    expect(screen.getByTestId("identity-group-claim")).toHaveTextContent("platform-admins");
    expect(screen.getByTestId("identity-no-roles")).toHaveTextContent("Dein Token enthielt keine Rolle");
  });
});
