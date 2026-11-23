import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { EntityCard, secretUsers } from "../EntityCard";
import type { ConfigCatalog } from "../useConfigCatalog";

// 2026-11-18-dafc: a secret named by a registry token is in use.
const CATALOG = {
  agents: [],
  trackers: [{ id: "jira", type: "jira", authSecret: "JIRA_TOKEN" }],
  connections: [],
  repos: [],
  projects: [],
  "mcp-servers": [],
  "design-sources": [],
  secrets: [],
} as unknown as ConfigCatalog;

const REGISTRIES = [
  { host: "npm.example.com", username: "", token: "${FEED_TOKEN}" },
  { host: "nuget.example.com", username: "ci", token: " PLAIN_TOKEN " },
];

describe("secretUsers", () => {
  it("names a registry whose token is ${id} or the bare id", () => {
    expect(secretUsers(CATALOG, REGISTRIES, "FEED_TOKEN")).toEqual(["registry npm.example.com"]);
    expect(secretUsers(CATALOG, REGISTRIES, "PLAIN_TOKEN")).toEqual(["registry nuget.example.com"]);
  });

  it("does not match a secret whose id is only part of a token", () => {
    expect(secretUsers(CATALOG, REGISTRIES, "FEED")).toEqual([]);
  });

  it("keeps the catalog users", () => {
    expect(secretUsers(CATALOG, REGISTRIES, "JIRA_TOKEN")).toEqual(["jira"]);
  });
});

describe("secret card", () => {
  it("reads 'used by registry <host>' for a registry token", () => {
    render(
      <EntityCard kind="secrets" entity={{ id: "FEED_TOKEN" }} catalog={CATALOG} onEdit={() => {}} registries={REGISTRIES} />,
    );
    expect(screen.getByTestId("config-secret-redaction-FEED_TOKEN")).toHaveTextContent("used by registry npm.example.com");
  });

  // 2026-11-23-78d7: the card says where the value comes from.
  const stored = { name: "deploy_token", setBy: "ada", setAt: "2026-11-17T10:00:00Z", keyId: "env:1", keyCurrent: true, readable: true };
  const source = (state: Parameters<typeof EntityCard>[0]["secretState"]) => {
    render(<EntityCard kind="secrets" entity={{ id: "DEPLOY_TOKEN" }} catalog={CATALOG} onEdit={() => {}} secretState={state} />);
    return screen.queryByTestId("config-secret-source-DEPLOY_TOKEN")?.textContent ?? null;
  };

  it("names who stored the value and when", () => {
    expect(source({ name: "DEPLOY_TOKEN", variable: "DEPLOY_TOKEN", fromEnvironment: false, stored })).toBe(
      ` · stored, set by ada on ${new Date(stored.setAt).toLocaleDateString()}`,
    );
  });

  it("names the environment variable when the value comes from there", () => {
    expect(source({ name: "DEPLOY_TOKEN", variable: "CI_DEPLOY_TOKEN", fromEnvironment: true, stored: null })).toBe(
      " · from the environment ($CI_DEPLOY_TOKEN)",
    );
  });

  it("says the environment wins over a stored value", () => {
    expect(source({ name: "DEPLOY_TOKEN", variable: "DEPLOY_TOKEN", fromEnvironment: true, stored })).toBe(
      " · from the environment ($DEPLOY_TOKEN), which wins over the stored value",
    );
  });

  it("says a stored value the current key cannot open", () => {
    expect(source({ name: "DEPLOY_TOKEN", variable: "DEPLOY_TOKEN", fromEnvironment: false, stored: { ...stored, readable: false } }))
      .toBe(" · stored, but the current master key cannot open it");
  });

  it("says no value yet when neither is set", () => {
    expect(source({ name: "DEPLOY_TOKEN", variable: "DEPLOY_TOKEN", fromEnvironment: false, stored: null })).toBe(" · no value yet");
  });

  it("claims no source while the state is unknown", () => {
    expect(source(null)).toBeNull();
  });

  it("still reads 'not referenced yet' when nothing names it", () => {
    render(<EntityCard kind="secrets" entity={{ id: "UNUSED" }} catalog={CATALOG} onEdit={() => {}} />);
    expect(screen.getByTestId("config-secret-redaction-UNUSED")).toHaveTextContent("not referenced yet");
  });
});
