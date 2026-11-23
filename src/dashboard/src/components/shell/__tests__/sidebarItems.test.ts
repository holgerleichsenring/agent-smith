import { describe, expect, it } from "vitest";
import type { CallerIdentity } from "@/lib/identityApi";
import { sidebarItems } from "../sidebarItems";

// 2026-11-23-933da: which sidebar items a caller sees, from the known identity and the PoCs read.

function identity(permissions: string[], projectPermissions?: Record<string, string[]>): CallerIdentity {
  return {
    authenticated: true, subject: "s", issuer: "i", roleClaim: "roles", groupClaim: "groups",
    roleClaimValues: [], groupClaimValues: [], roles: [], permissions, findings: [], projectPermissions,
  };
}

const EVERYTHING = [
  "new-work", "work-it-out", "series", "your-move", "tickets", "runs", "pull-requests",
  "insight", "overview", "delivery", "recent",
];

describe("sidebarItems", () => {
  it("sidebarItems_UnknownIdentity_ShowsEveryItem", () => {
    expect(sidebarItems(null, "unknown")).toEqual(EVERYTHING);
  });

  it("sidebarItems_PocsOn_PutsPocsRightAfterPullRequests", () => {
    const items = sidebarItems(null, "on");
    expect(items.indexOf("pocs")).toBe(items.indexOf("pull-requests") + 1);
  });

  it("sidebarItems_PocsOffOrForbidden_ShowsNoPocsItem", () => {
    expect(sidebarItems(null, "off")).not.toContain("pocs");
    expect(sidebarItems(identity(["runs.read", "dialog.write"]), "forbidden")).not.toContain("pocs");
  });

  it("sidebarItems_PocOnlyIdentity_ShowsOnlyPocs_NoNewWorkNoRecent", () => {
    const pocOnly = identity(["pocs.read", "pocs.write", "identity.read"], { "poc-ann": ["runs.read", "runs.watch"] });
    expect(sidebarItems(pocOnly, "on")).toEqual(["pocs"]);
    expect(sidebarItems(pocOnly, "off")).toEqual([]);
  });

  it("sidebarItems_WithoutDialogWriteAnywhere_HidesWorkItOut", () => {
    const reader = identity(["runs.read"]);
    expect(sidebarItems(reader, "unknown")).toEqual(EVERYTHING.filter((item) => item !== "work-it-out"));
  });

  it("sidebarItems_DialogWriteInOneProject_KeepsWorkItOut", () => {
    expect(sidebarItems(identity(["runs.read"], { alpha: ["dialog.write"] }), "unknown")).toContain("work-it-out");
  });

  it("sidebarItems_WithoutRunsReadAnywhere_HidesTheWorkAndInsight", () => {
    expect(sidebarItems(identity(["config.read", "config.write"]), "unknown")).toEqual(["new-work", "recent"]);
  });
});
