# Onboarding — from zero to running locally

**Target: 20 minutes on a clean machine.** If it takes you longer, that is a bug in this
document — open an issue.

This is the only setup document. `docs/database.md` covers migrations and production; come
back to it after this works.

---

## 0. What you need installed

| Tool | Version | Check |
| --- | --- | --- |
| .NET SDK | 10.0.x (pinned in `global.json`) | `dotnet --list-sdks` |
| Node.js | 22 LTS | `node --version` |
| Docker Desktop | any current | `docker compose version` |
| Git | any current | `git --version` |

You do **not** need SQL Server installed. Docker provides it.
You do **not** need an Azure account to develop. Only to deploy.

## 1. Clone both repositories

They are separate on purpose — see [`adr/0001-two-repositories.md`](adr/0001-two-repositories.md).

```bash
git clone https://github.com/Gio-Mtz/TacoTuesdayApi.git
git clone https://github.com/Gio-Mtz/TacoTuesdayUI.git
```

## 2. Start the database

From the API repository root:

```bash
cp .env.example .env          # Windows: copy .env.example .env
```

Open `.env` and set your own `MSSQL_SA_PASSWORD`. It needs 8+ characters with uppercase,
lowercase, a digit and a symbol — SQL Server refuses weak ones and the container dies with the
reason buried in its logs.

```bash
docker compose up -d
docker compose ps             # wait until STATUS says (healthy)
```

The first run downloads about 1.5 GB. After that it starts in seconds.

**The healthcheck matters.** SQL Server reports the container as running well before it accepts
connections. Wait for `(healthy)` or your first `dotnet run` fails to connect and you will go
looking for a bug in the code.

> Port **14330**, not 1433. If you ever install SQL Server natively it takes 1433, and two
> things fighting over a port is a bad afternoon.

## 3. Tell the API where the database is

The app reads `ConnectionStrings:Leads` and **refuses to start without it**. That is deliberate
— see `docs/database.md`. Locally it goes in user secrets, which live outside the repository,
so there is no way to commit it by accident:

```bash
cd src/TacoTuesday.Api
dotnet user-secrets set "ConnectionStrings:Leads" "Server=localhost,14330;Database=TacoTuesdayLeads;User Id=sa;Password=<the password from your .env>;TrustServerCertificate=True;Encrypt=False"
cd ../..
```

`TrustServerCertificate=True` is for the container's self-signed certificate. It is correct
locally and **wrong in production**, where Azure SQL presents a real certificate.

## 4. Create the schema

```bash
dotnet tool install --global dotnet-ef --version 10.0.11   # once per machine

dotnet ef database update \
  --project src/Migrations/TacoTuesday.Migrations.SqlServer \
  --startup-project src/TacoTuesday.Api
```

The migrations are in their own project on purpose. If that surprises you, read
[`adr/0006-migrations-assembly.md`](adr/0006-migrations-assembly.md) before touching them.

## 5. Run the API

```bash
dotnet run --project src/TacoTuesday.Api
```

| Check | Expect |
| --- | --- |
| `https://localhost:7001/health/live` | `Healthy` — the process is up |
| `https://localhost:7001/health/ready` | `Healthy` — **the database answered** |
| `https://localhost:7001/scalar` | The API reference |

If `ready` returns 503, the API is running but cannot reach the database. Step 2 or 3 is wrong.

## 6. Run the UI

In another terminal, from the UI repository:

```bash
npm ci
npm start
```

Open `http://localhost:4200`.

The Angular dev server proxies `/api` to the API — see `proxy.conf.json`. That is why there is
no CORS problem in development and why there is one in production.

## 7. Prove it end to end

Submit the waiting-list form. Then:

```bash
docker compose exec sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "<your password>" -C \
  -d TacoTuesdayLeads -Q "SELECT Email, Kind, CreatedAtUtc FROM Leads"
```

Your address should be there. **That is the definition of "my environment works".**

---

## Daily commands

```bash
docker compose up -d                        # start the database
dotnet run --project src/TacoTuesday.Api    # API   (terminal 1)
npm start                                   # UI    (terminal 2, other repo)
dotnet test                                 # the whole suite
```

## Running the API in a container instead

To reproduce exactly what ships to Azure — worth doing before blaming the cloud:

```bash
docker compose --profile parity up -d --build
curl http://localhost:5001/health/ready
```

Note Scalar is **absent** there: the Dockerfile sets `ASPNETCORE_ENVIRONMENT=Production` and
the API reference is behind `IsDevelopment()`. That is correct, not a bug.

---

## When it does not work

| Symptom | Cause |
| --- | --- |
| `docker compose up` exits immediately | Password too weak, or `.env` missing. `docker compose logs sql` |
| API: "connection string ... is required" | Step 3 not done, or done in the wrong folder |
| `/health/ready` is 503 | Database unreachable: wrong port, container not healthy, or schema missing |
| `ng build` fails after pulling | `npm ci` — someone changed a dependency |
| Port 14330 in use | Another compose stack is running. `docker compose down` in it |
| Tests pass locally, CI red | You forgot `dotnet ef migrations has-pending-model-changes`. See `docs/database.md` |

Start clean at any point:

```bash
docker compose down -v      # deletes the volume — all local data goes
docker compose up -d
# then repeat step 4
```
