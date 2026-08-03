import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, beforeEach } from "vitest";
import { PaneResizeHandle } from "../PaneResizeHandle";
import { usePersistedPaneWidth } from "@/hooks/usePersistedPaneWidth";

// p0395: the trace drawer's resize mechanics — a pointer-captured handle feeds
// clientX into a persisted pane width. Dragging resizes, releasing stops the
// drag, and the width survives a remount via localStorage.
//
// p0395a: the persisted value is a FRACTION of the basis (viewport / drawer),
// so a wider basis yields a proportionally wider pane; legacy absolute-pixel
// values (> 1) are migrated against the basis at load.

const KEY = "test.pane.width";

function Harness({ basis = 1000 }: { basis?: number }) {
  const [width, setWidth] = usePersistedPaneWidth(KEY, basis, 100, 2000);
  return (
    <div>
      <span data-testid="width">{width ?? "default"}</span>
      <PaneResizeHandle ariaLabel="Resize" testId="handle" onResize={setWidth} />
    </div>
  );
}

// This jsdom build ships no localStorage — back the hook with a Map for the test.
function stubStorage(): Map<string, string> {
  const backing = new Map<string, string>();
  Object.defineProperty(window, "localStorage", {
    configurable: true,
    value: {
      getItem: (k: string) => backing.get(k) ?? null,
      setItem: (k: string, v: string) => {
        backing.set(k, v);
      },
      removeItem: (k: string) => {
        backing.delete(k);
      },
      clear: () => {
        backing.clear();
      },
    },
  });
  return backing;
}

let storage: Map<string, string>;

beforeEach(() => {
  storage = stubStorage();
});

// jsdom has no PointerEvent: fireEvent's pointer events carry no clientX, so
// the moves are dispatched as MouseEvent-backed pointer events instead.
function pointerMove(el: HTMLElement, clientX: number) {
  fireEvent(el, new MouseEvent("pointermove", { clientX, bubbles: true }));
}

describe("PaneResizeHandle + usePersistedPaneWidth", () => {
  it("uses the stylesheet default until the operator drags", () => {
    render(<Harness />);

    expect(screen.getByTestId("width")).toHaveTextContent("default");
  });

  it("dragging resizes and persists the width as a fraction of the basis", () => {
    render(<Harness />);
    const handle = screen.getByTestId("handle");

    fireEvent.pointerDown(handle, { pointerId: 1, clientX: 300 });
    pointerMove(handle, 420);

    expect(screen.getByTestId("width")).toHaveTextContent("420");
    expect(storage.get(KEY)).toBe("0.42");
  });

  it("releasing the pointer ends the drag", () => {
    render(<Harness />);
    const handle = screen.getByTestId("handle");

    fireEvent.pointerDown(handle, { pointerId: 1, clientX: 300 });
    pointerMove(handle, 420);
    fireEvent.pointerUp(handle, { pointerId: 1 });
    pointerMove(handle, 900);

    expect(screen.getByTestId("width")).toHaveTextContent("420");
  });

  it("a stored fraction is applied on mount", async () => {
    storage.set(KEY, "0.333");

    render(<Harness />);

    expect(await screen.findByText("333")).toBeInTheDocument();
  });

  it("PersistedPaneWidth_StoresFraction_GrowsWithViewport", () => {
    const { rerender } = render(<Harness basis={1000} />);
    const handle = screen.getByTestId("handle");

    fireEvent.pointerDown(handle, { pointerId: 1, clientX: 300 });
    pointerMove(handle, 420);
    rerender(<Harness basis={1400} />);

    // Same stored fraction (0.42), wider basis — the pane widens on its own.
    expect(storage.get(KEY)).toBe("0.42");
    expect(screen.getByTestId("width")).toHaveTextContent("588");
  });

  it("PersistedPaneWidth_MigratesLegacyPxValue", async () => {
    // Legacy p0395 format: absolute pixels (always > 1).
    storage.set(KEY, "333");

    render(<Harness basis={1000} />);

    // The operator's dragged size is preserved and re-stored as a fraction.
    expect(await screen.findByText("333")).toBeInTheDocument();
    expect(storage.get(KEY)).toBe("0.333");
  });

  it("PersistedPaneWidth_ClampsDerivedPixels_ToBounds", async () => {
    storage.set(KEY, "0.05"); // 50px of a 1000px basis — below the 100px floor

    render(<Harness basis={1000} />);

    expect(await screen.findByText("100")).toBeInTheDocument();
  });
});
