import type { ReactNode } from "react";

// p0343d: the parity page head every system/rollup page opens with — the
// mock's .m-head title row (title + one-line subtitle in the established
// voice) with an optional right-side slot (connection pill, primary action).

export function PageHead({ title, sub, right }: { title: string; sub: string; right?: ReactNode }) {
  return (
    <div className="m-head">
      <div className="mt">
        <h1>{title}</h1>
        <div className="msub">{sub}</div>
      </div>
      {right}
    </div>
  );
}
