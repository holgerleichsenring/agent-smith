import { describe, it, expect } from "vitest";
import { projectIntegrity, resolves } from "../integrity";
import type { ConfigCatalog } from "../useConfigCatalog";
import type { StudioProject } from "@/lib/configApi";

const catalog: ConfigCatalog = {
  agents: [{ id: "gpt5", provider: "openai", models: { coding: "c", scan: "s" }, keySecret: "K" }],
  trackers: [{ id: "azdo", type: "azure", org: "o", project: "p", authSecret: "T" }],
  repos: [
    { id: "web", name: "web", branch: "main" },
    { id: "api", name: "api", branch: "main" },
  ],
  projects: [],
  "mcp-servers": [],
  secrets: [{ id: "K" }, { id: "T" }],
};

const project = (over: Partial<StudioProject>): StudioProject => ({
  id: "proj",
  agent: "",
  tracker: "",
  repos: [],
  trigger: "",
  pipelines: [],
  ...over,
});

describe("integrity", () => {
  it("Resolves_KnownId_ReturnsTrue", () => {
    expect(resolves(catalog, "agents", "gpt5")).toBe(true);
    expect(resolves(catalog, "agents", "missing")).toBe(false);
    expect(resolves(catalog, "agents", "")).toBe(false);
  });

  it("ProjectIntegrity_AllRefsResolve_Ok", () => {
    const i = projectIntegrity(catalog, project({ agent: "gpt5", tracker: "azdo", repos: ["web"] }));
    expect(i.ok).toBe(true);
    expect(i.agentOk && i.trackerOk && i.reposOk).toBe(true);
  });

  it("ProjectIntegrity_UnknownAgent_NotOk", () => {
    const i = projectIntegrity(catalog, project({ agent: "ghost", tracker: "azdo", repos: ["web"] }));
    expect(i.agentOk).toBe(false);
    expect(i.ok).toBe(false);
  });

  it("ProjectIntegrity_NoRepos_NotOk", () => {
    const i = projectIntegrity(catalog, project({ agent: "gpt5", tracker: "azdo", repos: [] }));
    expect(i.reposOk).toBe(false);
    expect(i.ok).toBe(false);
  });

  it("ProjectIntegrity_UnknownRepoInSet_NotOk", () => {
    const i = projectIntegrity(catalog, project({ agent: "gpt5", tracker: "azdo", repos: ["web", "ghost"] }));
    expect(i.reposOk).toBe(false);
    expect(i.repoResults.find((r) => r.id === "ghost")?.ok).toBe(false);
    expect(i.ok).toBe(false);
  });
});
