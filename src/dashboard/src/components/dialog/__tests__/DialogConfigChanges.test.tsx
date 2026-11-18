import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { DialogConfigChanges } from "../DialogConfigChanges";
import { summaryOf } from "../approvalDecision";
import type { SpecDialogProposalPush } from "@/types/spec-dialog";

describe("the admin assistant's configuration draft", () => {
  it("shows each change with what the store holds and what it will hold", () => {
    render(<DialogConfigChanges changes={[
      { operation: "upsert", kind: "trackers", id: "jira-b", document: '{"id":"jira-b"}', before: null },
      { operation: "delete", kind: "agents", id: "old", before: '{"id":"old"}' },
    ]} />);

    expect(screen.getByText("Create trackers/jira-b")).toBeTruthy();
    expect(screen.getByText("Delete agents/old")).toBeTruthy();
    expect(screen.getAllByTestId("dialog-config-change")).toHaveLength(2);
  });

  it("asks to apply, not to file", () => {
    const proposal = { kind: "config", children: [], configChanges: [{ operation: "upsert", kind: "trackers", id: "a" }] } as unknown as SpecDialogProposalPush;

    expect(summaryOf(proposal)).toContain("Apply this configuration change");
  });
});
