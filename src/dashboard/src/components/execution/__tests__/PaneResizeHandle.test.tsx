import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, beforeEach } from "vitest";
import { PaneResizeHandle } from "../PaneResizeHandle";
import { usePersistedPaneWidth } from "@/hooks/usePersistedPaneWidth";

// p0395: the trace drawer's resize mechanics — a pointer-captured handle feeds
// clientX into a persisted pane width. Dragging resizes, releasing stops the
// drag, and the width survives a remount via localStorage.

const KEY = "test.pane.width";

function Harness() {
  const [width, setWidth] = usePersistedPaneWidth(KEY);
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

  it("dragging resizes and persists the width", () => {
    render(<Harness />);
    const handle = screen.getByTestId("handle");

    fireEvent.pointerDown(handle, { pointerId: 1, clientX: 300 });
    pointerMove(handle, 420);

    expect(screen.getByTestId("width")).toHaveTextContent("420");
    expect(storage.get(KEY)).toBe("420");
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

  it("a stored width is applied on mount", async () => {
    storage.set(KEY, "333");

    render(<Harness />);

    expect(await screen.findByText("333")).toBeInTheDocument();
  });
});
