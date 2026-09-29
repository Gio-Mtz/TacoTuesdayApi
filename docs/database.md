# The database

The waiting list lives in Azure SQL. This file is the whole operational story: how the schema
gets there, how the app is told where it is, and what to do when something is wrong.

Everything here is a command **Gio runs**, because Claude has no .NET SDK and no `az`.

---

## One-time setup

```bash
dotnet tool install --global dotnet-ef
```

If it is already installed:

```bash
dotnet tool update --global dotnet-ef
```

---

## Creating the first migration

The model lives in `TacoTuesday.Modules.Leads`; the provider lives in the host. So `dotnet ef`
needs to be told both — the project that holds the `DbContext`, and the startup project that
knows it is SQL Server.

Run this from the repository root:

```bash
dotnet ef migrations add InitialLeads \
  --project src/Modules/TacoTuesday.Modules.Leads \
  --startup-project src/TacoTuesday.Api \
  --output-dir Persistence/Migrations
```

It writes three files under `src/Modules/TacoTuesday.Modules.Leads/Persistence/Migrations/`:
the migration, its designer file, and the model snapshot.

**This step is deliberately not done for you.** A migration and, more importantly, a model
snapshot are generated artefacts: EF produces them from the model, deterministically, in a
format that is tied to the EF version. A hand-written snapshot that is subtly wrong does not
fail — it makes the *next* migration generate a wrong diff, months later, against a table with
real rows in it. One command here is cheaper than that.

### What it should produce

If the mapping in `LeadConfiguration.cs` is what it should be, the generated `Up()` is a single
`CreateTable` for `Leads` plus one `CreateIndex`, and reads roughly like this:

| Column | Type | Null |
| --- | --- | --- |
| `Id` | `uniqueidentifier` | no — primary key, **no default**, the app supplies a UUID v7 |
| `Kind` | `nvarchar(16)` | no — the text `Company` or `Candidate`, not a number |
| `Name` | `nvarchar(80)` | no |
| `Email` | `nvarchar(160)` | no — as typed |
| `NormalizedEmail` | `nvarchar(160)` | no — lowercase |
| `Company` | `nvarchar(80)` | yes |
| `Role` | `nvarchar(80)` | yes |
| `CreatedAtUtc` | `datetimeoffset` | no |

plus

```sql
CREATE UNIQUE INDEX UX_Leads_NormalizedEmail ON Leads (NormalizedEmail);
```

**Three things to check before committing it**, because each one is a real bug if it is wrong:

1. The index is `UNIQUE` and it is on `NormalizedEmail`, **not** on `Email`. Without the
   `UNIQUE`, `EfCoreLeadStore` never takes the duplicate branch and the same address goes on
   the list twice. On `Email` instead, `Ana@x.com` and `ana@x.com` become two people.
2. `Id` has **no** `defaultValueSql: "newid()"`. If EF decided to generate the key, the UUID v7
   ordering is gone and the clustered index fragments.
3. `Kind` is `nvarchar`, not `int`.

---

## Putting the schema on the server

Two ways. Prefer the script for anything that is not your own laptop.

**Direct** — fine for a database only you are pointing at:

```bash
dotnet ef database update \
  --project src/Modules/TacoTuesday.Modules.Leads \
  --startup-project src/TacoTuesday.Api \
  --connection "<the real connection string>"
```

**As a script** — what a deploy should use, because you get to read it first and it can be run
twice without harm:

```bash
dotnet ef migrations script --idempotent \
  --project src/Modules/TacoTuesday.Modules.Leads \
  --startup-project src/TacoTuesday.Api \
  --output artifacts/leads-schema.sql
```

Then run `artifacts/leads-schema.sql` against the database from Azure Data Studio or `sqlcmd`.

### Why the app does not migrate itself on startup

It is one line and it is tempting, and it is wrong here for two reasons. The Container App can
run more than one replica: whichever boots first takes a schema lock and the others fail their
readiness probe while they wait. And it would mean the login the app runs as needs permission
to alter the schema **forever**, so that it can do something that happens once per release.

A migration is a deploy step.

---

## Telling the app where the database is

The app reads `ConnectionStrings:Leads`. In a container that is the environment variable:

```
ConnectionStrings__Leads
```

(two underscores — that is how .NET spells a nested configuration key in an environment
variable.)

It belongs in a Container Apps **secret**, not in `appsettings.json` and not in the image.

**If it is missing, the app refuses to start**, with a message that says so. That is on
purpose: the alternative is a container that starts, passes liveness, and throws a 500 at the
first person who types their address into the form.

---

## Checking it worked

```bash
curl -s https://<fqdn>/health/live    # the process is up. Runs no checks.
curl -s https://<fqdn>/health/ready   # "Healthy" only if the database answers.
```

`/health/ready` runs the `leads-db` check. While the database is unreachable it answers **503**
and Container Apps stops routing to that replica — which is right, and different from
restarting it: restarting a healthy process does not wake a sleeping database.

There is no `/health`. It 404s by design.

Then the real test, end to end:

```bash
curl -i -X POST https://<fqdn>/api/leads \
  -H "Content-Type: application/json" \
  -d '{"kind":"company","name":"Ana","email":"ana@acme.com","company":"Acme","role":null}'
```

- First call: **201**, with `"alreadyRegistered": false`.
- Same address again, any capitalisation: **200**, `"alreadyRegistered": true`, **same `id`**.
- Never a 409. The UI is written for this exact contract.
- Six calls inside a minute from the same IP: the sixth is **429** with `Retry-After`.

---

## Azure SQL serverless, and the first signup after a quiet night

If the database is on the serverless tier it pauses when idle and takes a few seconds to wake
up — and the connection that wakes it is the one that gets dropped. `EnableRetryOnFailure` in
`Program.cs` is what keeps that from being a failed signup. Do not remove it; the visitor did
nothing wrong and would have no idea why the form said no.

---

## Keeping the model and the migrations honest

This is the check worth putting in CI:

```bash
dotnet ef migrations has-pending-model-changes \
  --project src/Modules/TacoTuesday.Modules.Leads \
  --startup-project src/TacoTuesday.Api
```

It fails when somebody changes `LeadConfiguration.cs` and forgets the migration — which is the
one failure mode the test suite genuinely cannot see. The integration tests run on SQLite and
create their schema with `EnsureCreated()`, so they check the **model**; nothing but this
command checks that the **migration** still matches it.
