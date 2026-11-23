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
  "new-work", "work-it-out", "series", "your-move", "tickets", "runs", "pull-requests", "deployments",
  "insight", "overview", "delivery", "recent",
];

describe("sidebarItems", () => {
  it("sidebarItems_UnknownIdentity_ShowsEveryItem", () => {
    expect(sidebarItems(null, "unknown")).toEqual(EVERYTHING);
  });

  // 2026-11-23-8df6a + 933da: Pull requests, Deployments, then PoCs.
  it("sidebarItems_PocsOn_OrdersPullRequestsDeploymentsPocs", () => {
    const items = sidebarItems(null, "on");
    const at = items.indexOf("pull-requests");
    expect(items.slice(at, at + 3)).toEqual(["pull-requests", "deployments", "pocs"]);
  });

  it("sidebarItems_DeploymentsGatedLikePullRequests", () => {
    expect(sidebarItems(identity(["runs.read"]), "unknown")).toContain("deployments");
    expect(sidebarItems(identity(["config.read"]), "unknown")).not.toContain("deployments");
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
