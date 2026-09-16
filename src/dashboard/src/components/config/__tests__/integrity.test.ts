import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { projectIntegrity, resolveRepoRef, resolves, unfinishedTemplates } from "../integrity";
import type { ConfigCatalog } from "../useConfigCatalog";
import type { StudioProject } from "@/lib/configApi";

const catalog: ConfigCatalog = {
  agents: [{ id: "gpt5", provider: "openai", models: { coding: { model: "c" }, scan: { model: "s" } }, keySecret: "K" }],
  trackers: [{ id: "azdo", type: "azure", organization: "o", project: "p", authSecret: "T" }],
  connections: [
    { id: "conn", type: "azure-devops", organization: "acme", project: "core", authSecret: "T", defaultBranch: "main" },
  ],
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
  pipeline: "",
  pipelines: [],
  resolution: null,
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

  // --- p0345b: connection-scoped refs ("conn/Name") ------------------------

  it("ResolveRepoRef_ConnScopedRef_ValidWhenConnectionExists", () => {
    expect(resolveRepoRef(catalog, "conn/Sample.Api")).toEqual({
      id: "conn/Sample.Api",
      ok: true,
      via: "connection",
    });
  });

  it("ResolveRepoRef_UnknownConnection_NotOk", () => {
    expect(resolveRepoRef(catalog, "ghost/Sample.Api").ok).toBe(false);
  });

  it("ResolveRepoRef_ConnRefWithoutRepoName_NotOk", () => {
    expect(resolveRepoRef(catalog, "conn/").ok).toBe(false);
  });

  it("ResolveRepoRef_PlainRef_StillResolvesAgainstRepos", () => {
    expect(resolveRepoRef(catalog, "web")).toEqual({ id: "web", ok: true, via: "repo" });
    expect(resolveRepoRef(catalog, "ghost").ok).toBe(false);
  });

  it("ProjectIntegrity_OperatorShape_ConnRefsOnly_EmptyReposCatalog_Ok", () => {
    // The operator's production config: connections + conn-scoped refs, and an
    // EMPTY repos catalog. Nothing may be flagged falsely dangling.
    const operatorCatalog: ConfigCatalog = { ...catalog, repos: [] };
    const i = projectIntegrity(
      operatorCatalog,
      project({ agent: "gpt5", tracker: "azdo", repos: ["conn/Sample.Api", "conn/Sample.Web"] }),
    );
    expect(i.repoResults.every((r) => r.ok)).toBe(true);
    expect(i.ok).toBe(true);
  });

  it("ProjectIntegrity_MixedPlainAndConnRefs_EachResolvesAgainstItsCatalog", () => {
    const i = projectIntegrity(
      catalog,
      project({ agent: "gpt5", tracker: "azdo", repos: ["web", "conn/Sample.Api", "ghost/X"] }),
    );
    expect(i.repoResults.map((r) => r.ok)).toEqual([true, true, false]);
    expect(i.ok).toBe(false);
  });

  it("UnfinishedTemplates_HasOneImplementation_SharedByEveryReader", () => {
    // 2026-09-16-bedc: this rule was written out four times — the card, the project form, the
    // template row and the graph — and splitting the card's count would have added a fifth.
    // Modelled on RefMatches_HasOneImplementation_SharedByInventoryAndPicker.
    const read = (rel: string) => readFileSync(fileURLToPath(new URL(rel, import.meta.url)), "utf8");
    const readers = ["EntityCard.tsx", "ProjectForm.tsx", "TemplateRow.tsx", "ProjectGraph.tsx"];

    for (const name of readers) {
      const source = read(`../${name}`);
      expect(source, `${name} reads the shared rule`).toContain('from "./integrity"');
      expect(source, `${name} declares no unfinished rule of its own`)
        .not.toMatch(/!\w+\.context \|\| !\w+\.project \|\| !\w+\.repo/);
    }
    const shared = read("../integrity.ts");
    expect(shared).toContain("export function isTemplateUnfinished");
    expect(shared).toContain("export function unfinishedTemplates");
  });

  it("UnfinishedTemplates_MissingAnyRequiredField_IsUnfinished", () => {
    const base = { context: "server", project: "refapp", repo: "api", templateContext: "server" };
    const p = (over: Record<string, string>) =>
      ({ id: "x", agent: "", tracker: "", repos: [], pipeline: "", pipelines: [],
         resolution: null, templates: [{ ...base, ...over }] }) as never;

    expect(unfinishedTemplates(p({}))).toEqual([]);
    expect(unfinishedTemplates(p({ context: "" }))).toEqual([0]);
    expect(unfinishedTemplates(p({ templateContext: "" }))).toEqual([0]);
    // The revision is optional and never makes a binding unfinished.
    expect(unfinishedTemplates(p({ revision: "" }))).toEqual([]);
  });
});