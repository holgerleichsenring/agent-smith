# Moving the database between providers

`agentsmith archive` copies an installation's whole run store into one file and back,
so you can replace SQLite with SQL Server (or the other way round) without losing a row.

It talks to the **database the config file names**, not to a running server — the case
this exists for is a store being replaced underneath an installation, and the neighbouring
case is an installation whose server will not start.

```bash
# 1. take the archive from the old store
agentsmith archive export store.zip --config /etc/agentsmith/agentsmith.yml

# 2. point persistence.provider / persistence.connection_string at the NEW database,
#    then create its schema
agentsmith database migrate --config /etc/agentsmith/agentsmith.yml

# 3. write the archive into it
agentsmith archive import store.zip --config /etc/agentsmith/agentsmith.yml
```

## What it will refuse

* **A schema that does not match.** The archive records the head migration by NAME, and
  an import into a database at a different schema stops before it writes anything. Run
  `database migrate` on the target first.
* **A target that already holds rows.** An import runs into an empty schema; it does not
  merge. It names the table that stopped it.
* Afterwards the row counts are read back and compared to the archive's manifest. A
  difference fails the import and nothing is committed — the whole copy is one transaction.

Supported ends are **SQLite** and **SQL Server**. Postgres and MySQL are named in the
provider enum but share the SQLite-typed migration set, so they are not claimed here.

## Where the file may be kept

The archive is **as confidential as the database itself**. It holds ticket text, model
prompts, artifacts and the configuration store's secrets in clear, and it is deliberately
not redacted — a redacted copy would not restore. Keep it where you would keep a database
backup: encrypted at rest, access-controlled, and off developer laptops and ticket
attachments. Delete it once the move is verified.

The YAML config export (`agentsmith config export`) is a different thing and stays what it
is: a curated, editable document, not a copy of a table.
