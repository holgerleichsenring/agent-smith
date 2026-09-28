# Legal Analysis

The **legal-analysis** pipeline reads a contract or other legal document and produces a structured analysis: clause-level risk, obligations per party, red flags and recommended next actions. It converts the document to Markdown and hands it to the **legal-analyst-master**, which reads it section by section and writes the report.

## Pipeline steps

| # | Command | What it does |
|---|---------|-------------|
| 1 | LoadCatalog | Pulls and verifies the skill catalog |
| 2 | PipelineNameInitializer | Stamps the pipeline name for master routing |
| 3 | AcquireSource | Copies the document into the run's sandbox, byte for byte |
| 4 | EnsurePrerequisites | Installs MarkItDown in the sandbox |
| 5 | BootstrapDocument | Converts the document to Markdown and classifies the contract type |
| 6 | LoadCodingPrinciples | Loads the principles that frame the analysis |
| 7 | LoadMemoryIndex | Loads the project's recorded memory |
| 8 | AgenticMaster | Runs the legal-analyst-master over the document |
| 9 | DeliverOutput | Delivers the report the master wrote |

## Document conversion

`BootstrapDocument` converts the input with [MarkItDown](https://github.com/microsoft/markitdown), inside the sandbox. It handles PDF, DOCX / DOC, XLSX, PPTX and HTML. A conversion that fails stops the run with the file name.

It then asks a model to classify the contract from its opening text:

| Contract type | German name | Covers |
|---------------|-------------|--------|
| `nda` | Geheimhaltungsvereinbarung | Non-disclosure agreements |
| `werkvertrag` | Werkvertrag | Work contracts (deliverable-based) |
| `dienstleistungsvertrag` | Dienstleistungsvertrag | Service contracts (effort-based) |
| `saas-agb` | SaaS-AGB | SaaS terms of service |
| `kaufvertrag` | Kaufvertrag | Purchase contracts |
| `mietvertrag` | Mietvertrag | Lease contracts |

## How the master analyzes

The `AgenticMaster` step loads the **legal-analyst-master** skill. It treats the document as untrusted content: it analyses what the document says and never follows instructions written into it. It does not modify files; it reads and writes a report.

1. **Read.** It reads the converted document. For a long one it first locates the structural sections (definitions, term, payment, IP, warranties, liability, termination, governing law) and reads them in order.
2. **Analyse.** For each clause it names the type (obligation, right, restriction, limit, exclusion, penalty and so on), the party affected, a risk level from Critical to Informational from the perspective of the party you represent (the customer, unless the goal says otherwise), and specific red flags such as unbounded liability, automatic renewal with a short opt-out, broad IP assignment or non-mutual indemnification. Non-obvious interpretation choices are recorded with `log_decision`.
3. **Synthesise.** It writes a report with the document type, key obligations per party, a risk register sorted by severity with section references and counter-proposals, the red flags with citations, and a recommended next action (sign, negotiate specific clauses, reject, escalate).

It cites the document section for every claim, names the jurisdiction when an interpretation depends on it, and marks general-pattern warnings as such rather than as findings in this document. A document of hundreds of pages can be split across sub-agents, one per major section; otherwise the master works alone.

The report is written in plain language. The master's methodology ships as the `legal-analyst-master` skill in the [agentsmith-skills](https://github.com/holgerleichsenring/agent-smith-skills) catalog; see [Skills Catalog](../../how-it-works/skills-catalog.md) to pin or override it.

## Running

```bash
agent-smith legal --source ./contracts/supplier-agreement.pdf
```

`--project` names the project from your config and defaults to `legal`. `--output` chooses how `DeliverOutput` delivers: `console` (the default) or `markdown`. Any other value is refused before the run starts. `--output-dir` names the directory the Markdown report is written to. `--dry-run` shows the pipeline without running it.

```bash
agent-smith legal --source ./contracts/supplier-agreement.pdf --output markdown --output-dir ./reports
```

## Delivery

`DeliverOutput` delivers the report the master wrote, and nothing else: every document the master created outside the run record, joined in the order it wrote them.

| `--output` | Where the report goes |
|------------|-----------------------|
| `console` | Printed to standard output |
| `markdown` | Written to `findings.md` in the output directory |

The output directory is `--output-dir` when you give one and it is writable, otherwise `/output` (the container mount), otherwise `./agentsmith-output`, otherwise the system temp directory. It is the same rule the security and API scans use.

A run whose master wrote no report fails at `DeliverOutput` with "No analysis report to deliver" rather than printing an empty result. The source document is left where it is.

!!! warning "No legal advice"
    Agent Smith identifies and describes. The output is an analytical aid, not legal counsel. Always have a qualified lawyer review the results.
