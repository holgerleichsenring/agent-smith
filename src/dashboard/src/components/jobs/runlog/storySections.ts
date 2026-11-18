// 2026-11-17-d6fbb: the sections that hold what the run read and wrote rather than the steps it took.
// 2026-11-17-6d9eb: the run page builds its sections in runLogSections; what stays is the id type the ticket page's
// inline log shares through SectionId.

export type StorySectionId = "ticket" | "plan" | "outcome";
