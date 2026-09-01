# p0505 — what the data toolchain actually did

The table was `tests/AgentSmith.PipelineHarness/Reports/data-toolchain/measured-commands.tsv`
— 123 rows: 8 candidate commands over 20 fixture variants (3 shapes x clean + 8 named
defects), each executed twice, on 2026-08-23. 2026-08-31-77a8 deleted the table and the
gate that read it along with the domain profile they served; the fixtures and the
measurement script stay, so re-running the script regenerates it.
This file carries the narrative and the two answers the phase owed, from observed
output rather than from a derived verdict.

Re-measure with `tools/measure-data-toolchain.sh`. Everything below is a fact about
ONE pinned toolchain, recorded per row in the table itself:

```
python:3.12-bookworm
  + pip install dbt-core==1.11.4 dbt-databricks==1.12.4 sqlfluff==3.4.2
                yamllint==1.37.1 check-jsonschema==0.35.0
  + databricks CLI v1.13.0 (Go, from the GitHub release zip — no pip distribution)
```

## Answer 1 — profiles.yml

A step-0 input, and it stayed one. Each dbt-bearing fixture root carries its own
`profiles.yml` with `type: databricks`, a `.invalid` host and an `env_var`-defaulted
token, and every measured dbt command pins `--profiles-dir .`.

Observed: `dbt parse` reached a complete manifest against that profile with no
workspace, no token and no reachable host — `dbt-databricks` 1.12.4 configures the
SDK at connection-open time, not at parse time, which is what the 1.9.6 floor in the
spec's decisions was about (databricks/dbt-databricks#941). The floor holds; the
current release is far above it.

```
19:33:13  Running with dbt=1.11.4
19:33:29  Registered adapter: databricks=1.12.4
19:33:30  Unable to do partial parsing because saved manifest not found. Starting full parse.
19:33:35  Performance info: /w/target/perf_info.json
PARSE_EXIT=0
```

Without the `--profiles-dir .` pin dbt falls back to `~/.dbt` and the table would
record the measuring machine instead of the fixture. That pin is why a green row
here is a green row for the next person.

## Answer 2 — workspace auth

`databricks bundle validate` resolves workspace authentication BEFORE it validates
anything, and reds on the CLEAN bundle fixture:

```
Warn: [hostmetadata] failed to fetch host metadata for https://adb-0000000000000000.0.example.invalid, will skip for 1m0s
Name: sample_bundle
Target: dev
Found 1 error
Error: failed during request visitor: default auth: cannot configure default credentials, ...
VALIDATE_EXIT=1
```

By this phase's own rule a command that reds on a clean fixture of its own shape is
a broken command, not a gate. It is measured and recorded; it is not declarable.

`databricks bundle schema` needs no workspace at all — it emitted an 893 KB JSON
Schema offline, exit 0. It cannot fail on a bad bundle, so it is a PRODUCER. The
gate the bundle shape actually has is `check-jsonschema` against that emitted
schema, which is pure python on the image we already have.

## The claim that did not survive

The spec's first decision states that `dbt parse` SUCCEEDS with uninstalled packages,
and used that to argue the deps-before-parse ordering was inverted. On dbt-core
1.11.4 the opposite is true — `dbt parse` refuses outright:

```
19:32:26  Encountered an error:
Compilation Error
  dbt found 1 package(s) specified in packages.yml, but only 0 package(s) installed
  in dbt_packages. Run "dbt deps" to install package dependencies.
PARSE_EXIT=2
```

That is recorded rather than engineered away: `dbt parse --profiles-dir .` stays in
the table on its own and is marked `broken-on-clean`, and the sequence a repository
with a `packages.yml` actually runs — `dbt deps --profiles-dir . && dbt parse
--profiles-dir .` — was added as its own candidate row. A fixture without
`packages.yml` would have made the standalone command green, and would have deleted
the `missing-package` defect class along with the finding.

## Which defect each command actually caught

Claimed-versus-observed, from the table. Three claims did not hold:

- `dbt deps && dbt parse` was claimed for the unresolved ref, the undefined macro and
  the schema orphan. It caught the **unresolved ref** —
  `Compilation Error / Model 'model.sample_analytics.orders' (models/orders.sql)
  depends on a node named 'stg_ordrs'` — and the tab. It did **not** catch the
  undefined macro (exit 0) and did **not** catch the schema orphan (exit 0): dbt
  reports a patch with no matching node as a warning, and an undefined macro only
  surfaces past parse.
- `sqlfluff lint` was claimed for SQL that does not parse in the databricks dialect,
  and caught it — `PRS | Found unparsable section: 'selectt` — and also reds on the
  undefined macro (`TMP | Undefined jinja template`), which is a second real class it
  was not credited with.
- `check-jsonschema` caught both bundle classes exactly as claimed:
  `Additional properties are not allowed ('unknown_job_key' was unexpected)` and
  `'not a number' is not of type 'integer'`.

The undefined macro therefore has NO dbt-side gate in this toolchain; sqlfluff is the
only command that reds on it.

## What the verdict column does NOT say

`verdict` classifies the NETWORKED pass only. Read it together with `network`:

| shape | command | verdict | network on clean |
|---|---|---|---|
| dbt, combined | `dbt deps --profiles-dir .` | declarable | **yes** |
| dbt, combined | `dbt deps --profiles-dir . && dbt parse --profiles-dir .` | declarable | **yes** |
| dbt, combined | `dbt parse --profiles-dir .` | broken-on-clean | no |
| dbt, combined | `sqlfluff lint --dialect databricks models` | declarable | no |
| bundle, combined | `check-jsonschema --schemafile <emitted> databricks.yml resources/sample_job.yml` | declarable | no |
| bundle, combined | `databricks bundle schema` | no-defect-detected (producer) | no |
| bundle, combined | `databricks bundle validate` | broken-on-clean | no |
| all three | `yamllint .` | linter | no |

The two `network: yes` rows go red on the CLEAN fixture with `--network none`,
because `dbt deps` fetches from hub.getdbt.com. Behind p0304's default-deny egress
allowlist they become broken-on-clean by this phase's own rule, and the successor
either allows the hub or declares `sqlfluff` and `check-jsonschema` only. That is
the surprise p0304 inherits as a recorded fact instead of discovering live.

`dbt parse` alone reds on every variant of both dbt-bearing shapes for the SAME
reason — the packages are not installed — so its red on `unresolved-ref` says
nothing about refs. That is why the deps-then-parse row exists.

## What the offline test cannot see

Stated in the spec's decisions and still true: a tool release changing behaviour
under an unchanged version pin, a hand-transcribed exit code, and a defect variant
whose failure is not the failure its row names. None of them is visible without the
toolchain. The offline test recomputes the fixture hashes, cross-checks shape and
variant coverage both ways, and recomputes the one derived column (`verdict`) — and
nothing else, because nothing else in a `dotnet test` run can be re-derived.

## The one question this phase could not answer

What the project analyzer emits as declared ci commands for a dbt repository. The
eval is built and wired, and it SKIPPED — no `AZURE_OPENAI_API_KEY` and no
`OPENAI_API_KEY` in the environment the phase was implemented in:

```
SKIP: no AZURE_OPENAI_API_KEY / OPENAI_API_KEY in env — the eval tier is paid-API and opt-in.
```

`Reports/data-toolchain/analyzer-ci-commands.md` is committed saying exactly that,
with the command that fills it in. It stays unanswered rather than guessed, and it is
the premise the successor's profile depends on: if the analyzer draws any build
command here, it wins and the profile's list is never reached.

## What this phase does NOT ship

The binding to a declaration. The successor writes `profiles/<name>/profile.yaml` in
agent-smith-skills and owes, on that side, the check that its declared list is a
subset of what this table marks `declarable`. Nothing mechanical connects the two
repositories.
