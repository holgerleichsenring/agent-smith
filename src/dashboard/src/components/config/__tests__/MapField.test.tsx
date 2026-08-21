import { describe, it, expect, vi } from "vitest";
import { useState } from "react";
import { render, screen, fireEvent } from "@testing-library/react";
import { MapField } from "../MapField";

// p0499: the map editor used to be a textarea of `key: value` lines, split at the
// FIRST colon. The operator's real "Pipeline by label" map keys ARE label names
// containing a colon, so the whole five-entry map parsed to ONE entry and saving
// the dialog wiped the tracker's routing. These are the operator's exact strings —
// a fixture with colon-free keys passes against the broken code and proves nothing.
const OPERATOR_MAP: Record<string, string> = {
  "agent-smith:init": "init-project",
  "agent-smith:bug": "fix-bug",
  "agent-smith:feature": "add-feature",
  "agent-smith:security-scan": "security-scan",
  "agent-smith:api-security-scan": "api-security-scan",
};

const TEST_ID = "form-field-pipelineFromLabel";

/** Stands in for EntityDrawer: holds the draft and feeds the emitted map back in,
 *  so these are round trips through the control rather than one-way writes. */
function Harness({
  initial,
  onEmit,
}: {
  initial: Record<string, string>;
  onEmit: (v: Record<string, string> | undefined) => void;
}) {
  const [values, setValues] = useState<Record<string, string> | undefined>(initial);
  return (
    <MapField
      label="Pipeline by label"
      values={values ?? {}}
      testId={TEST_ID}
      onChange={(v) => {
        setValues(v);
        onEmit(v);
      }}
    />
  );
}

function renderField(initial: Record<string, string>) {
  const onEmit = vi.fn();
  render(<Harness initial={initial} onEmit={onEmit} />);
  return onEmit;
}

const keyInput = (i: number) => screen.getByTestId(`${TEST_ID}-key-${i}`) as HTMLInputElement;
const valueInput = (i: number) => screen.getByTestId(`${TEST_ID}-value-${i}`) as HTMLInputElement;
const lastEmit = (fn: ReturnType<typeof vi.fn>) =>
  fn.mock.calls.at(-1)?.[0] as Record<string, string> | undefined;

describe("MapField", () => {
  it("MapField_ColonBearingKeys_RoundTripUnchanged", () => {
    const onEmit = renderField(OPERATOR_MAP);

    // Every entry is its own row, and a colon in a key is just a string.
    Object.entries(OPERATOR_MAP).forEach(([k, v], i) => {
      expect(keyInput(i)).toHaveValue(k);
      expect(valueInput(i)).toHaveValue(v);
    });

    // Touching the field at all is what destroyed the map before: onChange re-parsed
    // the whole textarea on every keystroke and handed back ONE collapsed entry. Edit
    // one value and put it back — the map that comes out is the map that went in.
    fireEvent.change(valueInput(4), { target: { value: "api-security-scan-v2" } });
    fireEvent.change(valueInput(4), { target: { value: "api-security-scan" } });

    expect(lastEmit(onEmit)).toEqual(OPERATOR_MAP);
  });

  it("MapField_EditingOneValue_LeavesEveryOtherEntryIntact", () => {
    const onEmit = renderField(OPERATOR_MAP);

    fireEvent.change(valueInput(1), { target: { value: "fix-bug-v2" } });

    expect(lastEmit(onEmit)).toEqual({ ...OPERATOR_MAP, "agent-smith:bug": "fix-bug-v2" });
  });

  it("MapField_EditingAKeyThatContainsAColon_KeepsTheWholeKey", () => {
    const onEmit = renderField(OPERATOR_MAP);

    fireEvent.change(keyInput(0), { target: { value: "agent-smith:initialise" } });

    const emitted = lastEmit(onEmit)!;
    expect(Object.keys(emitted)).toHaveLength(5);
    expect(emitted["agent-smith:initialise"]).toBe("init-project");
    expect(emitted["agent-smith:init"]).toBeUndefined();
  });

  it("MapField_NewEmptyRow_SurvivesUntilItsKeyIsTyped", () => {
    renderField({ "agent-smith:bug": "fix-bug" });

    fireEvent.click(screen.getByTestId(`${TEST_ID}-add`));

    // The new row has an empty key, so it cannot exist in the emitted record — which
    // is exactly why it has to live in the field's own state. A row that vanishes as
    // it is being typed is the milder half of the same defect.
    expect(keyInput(1)).toHaveValue("");
    fireEvent.change(keyInput(1), { target: { value: "agent-smith:feature" } });
    expect(keyInput(1)).toHaveValue("agent-smith:feature");
    expect(valueInput(1)).toHaveValue("");

    fireEvent.change(valueInput(1), { target: { value: "add-feature" } });
    expect(keyInput(0)).toHaveValue("agent-smith:bug");
    expect(valueInput(1)).toHaveValue("add-feature");
  });

  it("MapField_RemoveRow_DropsOnlyThatEntry", () => {
    const onEmit = renderField(OPERATOR_MAP);

    fireEvent.click(screen.getByTestId(`${TEST_ID}-remove-0`));

    const emitted = lastEmit(onEmit)!;
    expect(Object.keys(emitted)).toHaveLength(4);
    expect(emitted["agent-smith:init"]).toBeUndefined();
    expect(emitted["agent-smith:api-security-scan"]).toBe("api-security-scan");
  });

  it("MapField_EveryRowRemoved_EmitsUndefined", () => {
    const onEmit = renderField({ "agent-smith:bug": "fix-bug" });

    fireEvent.click(screen.getByTestId(`${TEST_ID}-remove-0`));

    expect(lastEmit(onEmit)).toBeUndefined();
  });
});
