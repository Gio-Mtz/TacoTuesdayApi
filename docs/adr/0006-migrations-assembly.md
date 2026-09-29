# ADR-0006 — Migrations live in a provider-specific assembly, not next to the DbContext

- **Status:** accepted
- **Date:** 2026-09-29
- **Extends:** [ADR-0005](0005-leads-persistence.md). Nothing it decided is reversed; this is
  the consequence of its Decision 3 ("the host picks the provider") that we did not see until
  the first migration was actually generated.

## Context

ADR-0005 put a rule in place and an architecture test behind it:

> A module may know it lives on a relational database. It may not know **which** one.

`TacoTuesday.Modules.Leads` therefore references `Microsoft.EntityFrameworkCore` and
`Microsoft.EntityFrameworkCore.Relational` and no provider. `Program.cs` passes
`UseSqlServer`, the tests pass `UseSqlite`, and `Modules_must_not_depend_on_a_database_provider`
fails the build if that ever drifts. That seam is not decoration: it is the only reason the
store's tests exercise a **real unique index**, which is the thing the whole duplicate-handling
design rests on. EF's in-memory provider does not enforce unique indexes.

On 29-sep-2026 the first migration was generated, following `docs/database.md` as written:

```bash
dotnet ef migrations add InitialLeads \
  --project src/Modules/TacoTuesday.Modules.Leads \
  --startup-project src/TacoTuesday.Api \
  --output-dir Persistence/Migrations
```

and the solution stopped compiling:

```
error CS0103: The name 'SqlServerModelBuilderExtensions' does not exist in the current context
  Persistence/Migrations/LeadsDbContextModelSnapshot.cs(23,13)
  Persistence/Migrations/20260929203424_InitialLeads.Designer.cs(26,13)
```

The two rules had collided, and the collision is structural rather than a slip:

- A **model** can be provider-agnostic. Ours is.
- A **migration** cannot. `migrations add` builds the model using the design-time provider and
  writes a snapshot in terms of that provider. For SQL Server the snapshot opens with
  `SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder)`.

So "generate the migration into the project that holds the `DbContext`" — EF's default, and
what the docs said — is exactly what the architecture rule forbids.

## Options considered

**A. Add `Microsoft.EntityFrameworkCore.SqlServer` to the module.** One line, compiles, and
throws away the reason ADR-0005 exists. The architecture test would go red, and the honest way
to make it green would be to delete the test. Rejected.

**B. Delete the offending line from the generated files.** It compiles, and for *this* table it
even produces identical DDL — there is no identity column in `Leads`; the key is a UUID v7 the
app supplies. Rejected anyway, and this is the interesting one: the next `migrations add`
regenerates the snapshot from scratch and the line comes straight back, so the "fix" has to be
re-applied by hand, from memory, every time — against a file whose entire job is to be an exact
machine-written record of the model. The failure mode is not a broken build, it is a snapshot
that no longer matches what EF thinks it wrote, and a *wrong diff months later* against a table
with real rows. ADR-0005 already refused to hand-write this file; hand-editing it is the same
refusal.

**C. Put the migrations in the host, `TacoTuesday.Api`.** It already references the provider
and it is already "the only project that knows it is SQL Server", so this compiles and is not
absurd. Rejected on smaller grounds: it puts schema history inside a web project, it means the
host has to be rebuilt to add a migration, and it gives the answer to "where is the schema"
the shape of "somewhere in the API project".

**D. A provider-specific migrations assembly.** Chosen.

## Decision

A new project, `src/Migrations/TacoTuesday.Migrations.SqlServer`:

- references `TacoTuesday.Modules.Leads` (for the `DbContext` and the model) and
  `Microsoft.EntityFrameworkCore.SqlServer` (for the generated snapshot),
- holds the migrations under `Leads/`, in namespace `TacoTuesday.Migrations.SqlServer.Leads`,
- is referenced by `TacoTuesday.Api` so that `dotnet ef` finds it through the startup project.

Both places that build options point EF at it:

```csharp
sql.MigrationsAssembly(SqlServerMigrations.AssemblyName);
```

— `Program.cs` at runtime and `LeadsDbContextFactory` at design time. The name comes from a
marker type in that assembly rather than a string literal, so renaming the project is a
compile error here instead of "no migrations were found" halfway through a deploy.

`InitialLeads` was **moved**, not regenerated: the only edit is its `namespace` line. The model
snapshot is byte-for-byte what `dotnet ef` produced, which is the property that matters.

### Naming

The project is `TacoTuesday.Migrations.SqlServer`, deliberately **not**
`TacoTuesday.Modules.*`. `ModuleBoundayTests` keeps an explicit list of module assemblies with
a comment calling it "the checklist"; a project that looks like a module but is not one is how
that checklist gets a wrong entry. The name also leaves room for a second module's migrations
in the same assembly, and for a second provider beside it, without renaming anything.

## Consequences

**Good**

- The architecture rule survives contact with a real migration, and the test that guards it
  stays meaningful rather than becoming the thing that gets deleted.
- Nothing generated is hand-maintained.
- "Which database is this?" still has exactly one answer per assembly.

**Costs, honestly**

- `dotnet ef` commands are longer and `--project` now points somewhere non-obvious. That is
  mitigated only by documentation — `docs/database.md` leads with it, and the CI comment says
  it again at the call site.
- One more project in the solution, which is one more thing to add a second provider to if we
  ever want one (a `TacoTuesday.Migrations.Postgres` beside it).
- `--namespace` has to be passed on every `migrations add`, or a future migration lands in a
  different namespace than its siblings for no reason. It is in the documented command.

**Unchanged**

- The tests. They build their schema with `EnsureCreated()` from the model on SQLite and never
  touch a migration — which is precisely why the CI job
  `migrations-match-the-model` exists, and why it is the only thing in the repository that can
  see a migration drifting away from `LeadConfiguration.cs`.

## Status of verification

`[requiere verificación de Gio]`. Claude has no .NET SDK and cannot reach the Microsoft
package hosts, so **none of this has been compiled.** What is claimed here is a reading of the
compiler error and of EF's generation model, not an observed green build. The judges are
`dotnet build -c Release`, `dotnet test`, and the two CI jobs.
