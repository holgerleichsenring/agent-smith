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

  it("still reads 'not referenced yet' when nothing names it", () => {
    render(<EntityCard kind="secrets" entity={{ id: "UNUSED" }} catalog={CATALOG} onEdit={() => {}} />);
    expect(screen.getByTestId("config-secret-redaction-UNUSED")).toHaveTextContent("not referenced yet");
  });
});
