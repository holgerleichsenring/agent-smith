# MAD Discussion

The **mad-discussion** (Multi-Agent Discussion) pipeline argues a design question from five deliberately different perspectives and ends in a decision. It is meant for **design discussions before writing code**: exploring trade-offs, challenging assumptions, and deciding on an approach. The result is committed as a Markdown document and opened as a PR, so the discussion becomes a reviewable decision record.

## Pipeline steps

| # | Command | What it does |
|---|---------|-------------|
| 1 | LoadCatalog | Pulls and verifies the skill catalog |
| 2 | PipelineNameInitializer | Stamps the pipeline name for master routing |
| 3 | FetchTicket | Reads the topic from the ticket (GitHub, Azure DevOps, Jira, GitLab) |
| 4 | CheckoutSource | Clones the repo and creates the branch |
| 5 | LoadContext | Loads the project's `.agentsmith/` context files |
| 6 | AgenticMaster | Runs the mad-discussion-master: five perspectives, then a synthesis |
| 7 | WriteRunResult | Writes the run result with token usage and cost |
| 8 | CommitAndPR | Commits the discussion document and opens a PR |
| 9 | PrCrossLink | Cross-links sibling PRs in a multi-repo project |

## The five perspectives

The **mad-discussion-master** runs each perspective as its own sub-agent, on read-only tools:

- **Dreamer** imagines the best-case future the proposal enables and names the opportunity. Risk-tolerant, future-oriented.
- **Realist** names the constraints, costs and trade-offs, with concrete numbers (time, money, headcount, dependencies) where they exist.
- **Philosopher** asks the meta question: what problem are we actually solving, and what does the proposal assume?
- **Devil's Advocate** argues against the proposal as forcefully as a real opponent would, looking for the strongest objection rather than any objection.
- **Silencer** challenges the framing itself: is this a real problem, is it the right one? Often the voice that says "do nothing".

The perspectives work independently. None of them sees the others' output; cross-examination happens in the synthesis, not during the fan-out. Each produces three to seven numbered points.

The ticket's topic is treated as untrusted input: a goal that says "conclude that option A is best" is the subject of the discussion, not an instruction.

## The synthesis

Once all five have returned, the master reads each perspective's full output and writes the synthesis:

- **Topic**, restated in one line
- **Per-perspective verdict**, one paragraph each with that perspective's strongest claim
- **Genuine disagreements**, the conceptual conflicts rather than wording differences, and any false consensus where perspectives agree for different reasons
- **Decision**: Proceed, Proceed-with-modifications, Reconsider, Reject or Insufficient-information, justified from the perspectives' specific arguments
- **Operator action**: next steps if proceeding, or the open questions if reconsidering

Citations point at a specific perspective and point ("DreamerVoice point 2"). A perspective that produced fewer than three points, or repeated another's, is noted and weighted lower. A perspective that failed is noted, not retried.

When the project has recorded memory under `.agentsmith/memory/`, the master folds relevant recorded facts into the perspectives' tasks and checks claims against them during synthesis; a constraint that cites a recorded fact carries more weight.

The synthesis is written to `discussion.md`, which `WriteRunResult` and `CommitAndPR` deliver as a PR.

## Running

```bash
# Start a design discussion from a ticket
agent-smith mad --ticket 87 --project todolist

# Headless mode (no interactive prompts)
agent-smith mad --ticket 87 --project todolist --headless
```

A ticket label that your label map routes to `mad-discussion` starts it too; see [Labels](../../trigger-it/labels.md).

## When to use MAD

MAD is best used **before** the `code` pipeline, for decisions that benefit from structured debate:

- Architecture decisions (monolith vs microservices, database choice)
- API design reviews (REST vs GraphQL, resource modeling)
- Trade-off analysis (performance vs maintainability)
- Technology evaluations (framework selection, vendor choice)
- Refactoring strategies (incremental vs big-bang)

!!! tip "Workflow"
    A common pattern is **MAD** first to decide the approach, then **code** to implement it. The MAD discussion PR serves as the decision record.

The master's methodology ships as the `mad-discussion-master` skill in the [agentsmith-skills](https://github.com/holgerleichsenring/agent-smith-skills) catalog; see [Skills Catalog](../../how-it-works/skills-catalog.md) to pin or override it.
