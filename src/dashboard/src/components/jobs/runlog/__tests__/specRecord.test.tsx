import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { CitedList } from "../CitedList";
import { deliveredAccounts, parseSpecRecord } from "../specRecord";
import { SpecRecordView } from "../SpecRecordView";

// 2026-11-18-e06bc: the executed spec read from its YAML into labelled lines; long citations collapse to "n files ›".

const DELIVERED = [
  "## What this spec delivers",
  "",
  "**shop**",
  "- [x] Every package is current — `shop/src/Api/Api.csproj; shop/src/Core/Core.csproj; shop/tests/Api.Tests/Api.Tests.csproj`",
  "- [x] The build passes — verified by command",
  "- [~] The worker is upgraded — not applicable: the base carries no worker",
  "- [ ] The client is current — client lags",
  "- measured: ~12 turns against a ceiling of 40",
].join("\n");

const RECORD = `spec: 2026-11-18-0001a
goal: "Upgrade every **package** of the shop."
scope:
  in: "Direct references in every project."
  out: "Framework majors."
steps:
  - id: inventory
    action: "List the packages."
  - id: server
    action: "Upgrade the \`api\` packages."
tests:
  - "Api_Build_Passes"
done:
  - "Every package is current."
  - "The build passes."
requires: [2026-11-01-0001a]
outcome:
  run: 2026-11-18T10-00-00-abcd
  delivered: |-
${DELIVERED.split("\n").map((l) => `    ${l}`).join("\n")}
`;

describe("specRecord", () => {
  it("parseSpecRecord_ReadsEveryPart", () => {
    const spec = parseSpecRecord(RECORD)!;
    expect(spec.goal).toBe("Upgrade every **package** of the shop.");
    expect(spec.scope).toEqual([{ label: "scope in", lines: ["Direct references in every project."] }, { label: "scope out", lines: ["Framework majors."] }]);
    expect(spec.steps).toEqual([{ id: "inventory", action: "List the packages." }, { id: "server", action: "Upgrade the `api` packages." }]);
    expect(spec.tests).toEqual(["Api_Build_Passes"]);
    expect(spec.done).toHaveLength(2);
    expect(spec.run).toBe("2026-11-18T10-00-00-abcd");
    expect(spec.other).toEqual([{ label: "requires", lines: ["2026-11-01-0001a"] }]);
    expect(spec.delivered[0].repo).toBe("shop");
  });

  it("parseSpecRecord_NotASpec_IsNull", () => {
    expect(parseSpecRecord("# A document\n\nwith prose")).toBeNull();
    expect(parseSpecRecord("key: [unclosed")).toBeNull();
  });

  it("deliveredAccounts_MarksCitationsCommandsAndMeasures", () => {
    const [account] = deliveredAccounts(DELIVERED);
    expect(account.rows).toEqual([
      { kind: "criterion", mark: "met", text: "Every package is current", citation: "shop/src/Api/Api.csproj; shop/src/Core/Core.csproj; shop/tests/Api.Tests/Api.Tests.csproj", note: null },
      { kind: "criterion", mark: "met", text: "The build passes", citation: null, note: "verified by command" },
      { kind: "criterion", mark: "na", text: "The worker is upgraded", citation: null, note: "not applicable: the base carries no worker" },
      { kind: "criterion", mark: "unmet", text: "The client is current", citation: null, note: "client lags" },
      { kind: "line", text: "measured: ~12 turns against a ceiling of 40" },
    ]);
  });

  it("SpecRecordView_LabelledLines_AndCollapsedCitations", () => {
    render(<SpecRecordView spec={parseSpecRecord(RECORD)!} />);
    expect(screen.getByTestId("spec-record-goal")).toHaveTextContent("goalUpgrade every package of the shop.");
    expect(screen.getByTestId("spec-record-scope-in")).toHaveTextContent("Direct references in every project.");
    expect(screen.getByTestId("spec-record-steps")).toHaveTextContent("inventoryList the packages.");
    const rows = screen.getAllByTestId("spec-delivered-criterion");
    expect(rows[0]).toHaveTextContent("Every package is currentproven3 files ›");
    expect(rows[1]).toHaveTextContent("The build passesproven by command");
    fireEvent.click(screen.getByTestId("cited-toggle"));
    expect(screen.getAllByTestId("cited-item").map((e) => e.textContent)).toEqual(["shop/…/Api.csproj", "shop/…/Core.csproj", "shop/…/Api.Tests.csproj"]);
    expect(screen.getAllByTestId("cited-item")[0]).toHaveAttribute("title", "shop/src/Api/Api.csproj");
  });

  it("CitedList_TwoItemsOrACommand_StayInline", () => {
    render(<CitedList text="shop/a/b/package.json; shop/package-lock.json" />);
    expect(screen.getByTestId("cited-inline")).toHaveTextContent("shop/…/package.json; shop/package-lock.json");
  });
});
