# Contributing to Agent Smith

Thanks for wanting to help. This is a draft; who decides what is in
[GOVERNANCE.md](GOVERNANCE.md).

## Spec first

Every change starts as a spec, before any code. The workflow — minting a spec id,
writing the spec, logging decisions, updating the record — is written down in
[CLAUDE.md](CLAUDE.md), and the
[spec-first plugin](https://github.com/holgerleichsenring/specification-first-agentic-development)
packages it as skills (`create-spec`, `review-spec`, `apply-spec`, `deliver-spec`)
for Claude Code. For anything larger than a typo, open an
[issue](https://github.com/holgerleichsenring/agent-smith/issues) first, so the
spec can be cut together.

## The spec gate

A commit whose message names a spec goes through the spec gate
(`.claude/hooks/spec-gate.sh`): the backend build, the full test suite, the
dashboard's build and tests, the CLI dry runs and the harness presets must be
green, or the commit is blocked. If you commit outside Claude Code, run the same
checks by hand before you push; `CLAUDE.md` lists them.

## Pull requests

Work on a branch named `spec/{id}`, after the spec it implements, and open the
pull request from it. One spec, one commit, with the spec id in the message.

## Language

Everything in the repository is English: code, comments, specs, decisions, docs
and commit messages.

## Developer Certificate of Origin

Every commit must carry a `Signed-off-by` line, which certifies that you wrote the
change or otherwise have the right to submit it under the
[Developer Certificate of Origin](https://developercertificate.org).
`git commit -s` adds the line for you:

```
Signed-off-by: Your Name <you@example.com>
```

Contributions are accepted under the project's [MIT license](LICENSE).
