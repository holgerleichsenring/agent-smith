"use client";

import { useEffect, useState } from "react";

// p0395a: the live viewport width — null until mounted (the server render has
// no window), then tracking resizes so fraction-persisted pane widths
// re-derive their pixel size as the window grows or shrinks.

export function useViewportWidth(): number | null {
  const [width, setWidth] = useState<number | null>(null);

  useEffect(() => {
    const update = () => {
      setWidth(window.innerWidth);
    };
    update();
    window.addEventListener("resize", update);
    return () => {
      window.removeEventListener("resize", update);
    };
  }, []);

  return width;
}
