# Project templates

Most estates I've seen have one project that is built the way everybody wants the next one to be built. The naming, the layering, where the tests go, how a slice of work is cut. A new service should look like that one, and "look at `acme-orders` and do it like that" is exactly the sentence nobody writes into a ticket.

A template is how you say it once, in configuration. A project declares that one of its contexts is *built after* a context of another project's repository, at a revision. The runs that design and write code for that context can then open the template and read it, read only, next to the code they're changing.

## Declaring one

In the studio it's the **templates** tab of the project drawer. In YAML it's a `templates:` list on the project:

```yaml
projects:
  todolist:
    agent: default-claude
    tracker: acme-issues
    repos: [todolist-api, todolist-web]
    templates:
      - context: api                  # a context of THIS project
        context_repo: todolist-api    # optional: which of this project's repos it lives in
        project: acme-orders          # the project the template belongs to
        repo: orders-api              # a repo ref of THAT project
        template_context: service     # the context inside that repo this one is built after
        revision: v2.4.0              # optional: a tag, a branch or a commit
```

| Key | What it names |
|---|---|
| `context` | the context of this project the binding applies to, as named under `.agentsmith/contexts/<name>/` in its repo |
| `context_repo` | the repo of this project that context lives in. Leave it out unless two of the project's repos declare a context of the same name; then it says which one you mean |
| `project` | the project the template belongs to |
| `repo` | one repo ref of that project, a catalog name or a `connection/Name` ref, exactly one repository |
| `template_context` | the context inside that repo this one is built after |
| `revision` | a tag, a branch or a commit sha. Empty means the template repo's default branch |

A project can declare several templates, one per context it wants built after something. Two declarations that address the same template are listed and read once.

## In the studio

The templates tab lists what the project declares, one row per binding, and opens one binding at a time for editing. Most of it is picked rather than typed:

- **context** is picked from the contexts of this project's repositories. The option says which repository declares it, and the picker always stores that repository as `context_repo`, so the wiring graph can place every context.
- **built after project** is picked from the project catalog, **its repo** from that project's repositories, **its context** from the contexts found in that repository. Both context lists are read from the repository's default branch.
- **revision** is typed ("a tag, a branch or a commit"). Nothing lists refs, so it's checked where the template is fetched.

A binding that is only half filled in is unfinished, and the drawer won't save while one is: the footer names it ("template 2 is unfinished — give it a context, a project, a repo and a template context"). The project card shows `built after acme-orders`, and the wiring graph draws each template in its own column next to the context it applies to.

## What is refused

These are checked when you save in the studio, on import, and when a configuration loads:

- a `project` that doesn't exist
- a `repo` that the named project doesn't carry
- a `repo` with a wildcard in it (a template is one repository, a wildcard names a set nobody enumerated)
- a `context_repo` that this project doesn't carry
- a cycle, a project built after a project that is (directly or not) built after it

And a project that another project names as its template can't be deleted while that declaration exists.

Context names are the one thing nothing checks at save time. Contexts live in the repositories, not in the configuration, so a mistyped `context` or `template_context` surfaces where the template is opened.

## Who reads a template

Three things open a template, and all three read it at the declared revision, through a read-only checkout of the other project's repository:

- **The derivation**, when a `code` run cuts a ticket into phases. It gets its own allowance of looks into templates, separate from the one it has for the repositories it's changing, so a template never eats the budget for the real code. The resolved commit is recorded with the derivation, and so is what the template declares as its own proof (the `verify` stages of its `context.yaml`, or that it declares none). That last part is reported, not enforced.
- **The coding master**, while it writes the code for a phase. The templates declared for the contexts the phase changes appear in its repository list with a `template:` prefix (`template:api`, or `template:todolist-api/api` when `context_repo` is set). It reads them with the same tools it reads a repository with. Writing into one, editing one or running a command in one is refused, and nothing in a template is ever committed or put in a pull request.
- **The design partner**, when you work out a feature in a design conversation. Every template the project declares is available to it, cloned only when it is actually read and then held for the rest of the conversation. The ticket it files names the templates it had open and the commit each was read at.

Scans don't open templates.

## When the template and the code disagree

A template answers *how* something is built. It never answers *what* to build; that's the phase spec or the feature under discussion. When sources disagree, the masters follow one order:

1. The project's principles win every collision.
2. The template gives the form for what is **new**, structure that has no counterpart in the code being changed.
3. The existing code gives the form for an **extension**, anywhere a counterpart already exists.
4. A prototype or a design gives the *what* and never the form.

The test between 2 and 3 is simple: does a counterpart already exist in the target? If it does, the target's own form wins, and a template doesn't get to rewrite working code the ticket didn't ask about.

## When a template can't be opened

A template that can't be opened stops the work rather than letting it go ahead without it. The derivation fails naming the template and the revision, and so does a template look refused because its allowance ran out. On the coding side the phase fails before the master starts. The failure says which of these it was:

- the revision doesn't exist
- the revision exists but isn't reachable from any branch or tag the clone fetched (a commit only on a pull request ref or a deleted branch)
- the host refused the credential
- the host couldn't be reached
- the connection names no clone URL

Agent Smith uses one token per platform. A template in another organisation that token can't read comes back as refused.

## Next

- [The Config studio](config-studio.md), the rest of the project drawer.
- [agentsmith.yml reference](../reference/configuration/agentsmith-yml.md#projects), every project key.
- [Context file](../reference/concepts/context-file.md), what a context declares.
