# Database access — the one-time DevOps setup

**Audience: whoever administers Azure.** Developers do not need this file; they need
[`onboarding.md`](onboarding.md), which is three commands long *because* of what is set up here.

This is done **once**. After it, adding a new developer is adding them to a group.

---

## The design, in one paragraph

Two databases on one logical server. Nobody anywhere has a database password — not the
developers, not the API, not the CI. Everything authenticates with **Microsoft Entra**:
developers with their own sign-in, the API with its managed identity. There is no secret to
store, rotate, leak or forget, which is why there is no Key Vault in this picture.

```
  tacotuesday-sql  (South Central US)
   ├── ttc-sqldb-dev    ← developers (group ttc-developers) + CI.  read/write/DDL
   └── ttc-sqldb-prod   ← ONLY the Container App's managed identity. read/write, NO DDL
```

### Why two databases and not one

Pointing local development at the production database looks simpler and costs you the product:

- Every developer's test rows land among real signups.
- One `dotnet ef database update` run from a laptop changes the production schema.
- The Azure SQL free offer gives **100,000 vCore-seconds per database per month**. Local
  development burns that quota, and when it runs out the database **auto-pauses until the next
  calendar month** — production goes down because somebody was debugging.

The second database is free: the offer covers up to 10 databases per subscription.

### Why no passwords, and no Key Vault either

A Key Vault is the right answer when a secret has to exist. Here none does. Entra hands out
short-lived tokens to identities that already exist — a person's work account, the Container
App's managed identity — so there is nothing to put in a vault.

That is also why the development connection string is **committed to the repository**: it
contains a server name and a database name. Both are public information. There is no key in it.

Key Vault comes back when something genuinely secret appears — an email provider's API key, an
analytics token. Not before.

---

## Setup

Run once, in order. Everything below assumes `az login` as an owner of the subscription.

### 1. The Entra administrator of the server

Already done — Gio is the Entra admin. Verify:

```bash
az sql server ad-admin list --resource-group rg-tacotuesday --server tacotuesday-sql -o table
```

Without this, none of the `FROM EXTERNAL PROVIDER` statements below will work.

### 2. The development database

```bash
az sql db create \
  --resource-group rg-tacotuesday \
  --server tacotuesday-sql \
  --name ttc-sqldb-dev \
  --edition GeneralPurpose --compute-model Serverless \
  --family Gen5 --capacity 2 \
  --use-free-limit true --free-limit-exhaustion-behavior AutoPause
```

`--use-free-limit` is what keeps it at zero cost. `AutoPause` means that if the monthly quota
is exhausted the database pauses instead of billing — on a development database that is exactly
what you want.

### 3. The developers group

```bash
az ad group create --display-name ttc-developers --mail-nickname ttc-developers

# add a person (repeat per developer — this is the whole onboarding from then on)
az ad group member add --group ttc-developers \
  --member-id $(az ad user show --id persona@dominio.com --query id -o tsv)
```

### 4. Grant the group access to the dev database

Connect to **`ttc-sqldb-dev`** (Azure Data Studio, SSMS, or the portal's Query editor) as the
Entra admin, and run:

```sql
CREATE USER [ttc-developers] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [ttc-developers];
ALTER ROLE db_datawriter ADD MEMBER [ttc-developers];
ALTER ROLE db_ddladmin   ADD MEMBER [ttc-developers];   -- so they can run migrations
```

`db_ddladmin` is deliberate on dev and deliberately **absent** on prod.

### 5. Grant the API access to the production database

Connect to **`ttc-sqldb-prod`** as the Entra admin. The name in brackets is the managed
identity's name, exactly as it appears in Azure:

```sql
CREATE USER [id-tacotuesday] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [id-tacotuesday];
ALTER ROLE db_datawriter ADD MEMBER [id-tacotuesday];
-- deliberately NO db_ddladmin: the app reads and writes rows, it does not change the schema.
-- Migrations are a deploy step, run by a human or a pipeline. See database.md.
```

If the Container App was created with a user-assigned identity, use that identity's name. If it
uses a system-assigned identity, the name is the Container App's own name.

Verify it landed:

```sql
SELECT name, type_desc, authentication_type_desc
FROM sys.database_principals WHERE name = 'id-tacotuesday';
```

`type_desc` should be `EXTERNAL_USER`. No row means the statement did not run, whatever the
editor showed.

#### If `FROM EXTERNAL PROVIDER` fails

In tenants whose Entra admin is a guest or a personal Microsoft account, the server cannot always
resolve the name through Graph and the statement fails with *Principal 'id-tacotuesday' could not
be found*. The managed identity is fine; only the lookup failed. Create the user from its client
id instead — same result, no Graph call:

```sql
DECLARE @appId uniqueidentifier = CAST('PEGA-AQUI-EL-CLIENT-ID' AS uniqueidentifier);
DECLARE @sql nvarchar(max) = N'CREATE USER [id-tacotuesday] WITH SID = '
    + CONVERT(nvarchar(100), CAST(@appId AS varbinary(16)), 1) + N', TYPE = E;';
EXEC sp_executesql @sql;

ALTER ROLE db_datareader ADD MEMBER [id-tacotuesday];
ALTER ROLE db_datawriter ADD MEMBER [id-tacotuesday];
```

`TYPE = E` is "external user". The `CAST(... AS varbinary(16))` is what converts the GUID to the
byte order SQL Server expects — doing it by hand is where this goes wrong.

### 6. Point the Container App at prod, without a password

The Container App authenticates as its **managed identity**. Name the identity outright; do not
leave it to a credential chain to guess.

```bash
RG=rg-tacotuesday; APP=tacotuesday-api; MI=id-tacotuesday

# the identity exists and is attached to the app (both commands are idempotent)
az identity create -n $MI -g $RG -o none
az containerapp identity assign -n $APP -g $RG \
  --user-assigned $(az identity show -n $MI -g $RG --query id -o tsv) -o none

CLIENT_ID=$(az identity show -n $MI -g $RG --query clientId -o tsv)
echo "clientId = $CLIENT_ID"   # needed again in step 5

az containerapp update -n $APP -g $RG --set-env-vars \
  "ConnectionStrings__Leads=Server=tcp:tacotuesday-sql.database.windows.net,1433;Initial Catalog=ttc-sqldb-prod;Authentication=Active Directory Managed Identity;User Id=$CLIENT_ID;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30"
```

This is an **environment variable, not a secret** — it contains no secret. If a Container App
secret was created for it earlier, it is now unreferenced and can be removed:

```bash
az containerapp secret remove -n $APP -g $RG --secret-names <nombre-del-secret>
```

#### Why this `Authentication` value and not another one

| Value | In a container |
|---|---|
| `Active Directory Managed Identity` + `User Id=<clientId>` | **Correct.** Names one identity, fails fast and clearly. |
| `Active Directory Default` | Works, but walks a chain of credential types; when the identity is missing or ungranted it spends 30+ seconds probing, turning a clear error into a timeout. |
| `Active Directory Interactive` | **Can never work.** It opens a browser window for the MFA prompt. There is no browser and no human inside a replica. It succeeds on a laptop for exactly that reason. |
| `User Id=persona@correo.com` | Wrong kind of principal. A person's sign-in is not a workload identity; it cannot be used unattended. |

### 7. Firewall

```bash
# let Azure services (the Container App) through
az sql server firewall-rule create --resource-group rg-tacotuesday --server tacotuesday-sql \
  --name AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0

# and each developer's IP
az sql server firewall-rule create --resource-group rg-tacotuesday --server tacotuesday-sql \
  --name dev-gio --start-ip-address <ip> --end-ip-address <ip>
```

The `0.0.0.0`–`0.0.0.0` rule is not "allow the internet": it is the special value meaning
*allow Azure-internal services*. It is still broad — any Azure tenant is inside it — which is
survivable only because the database also requires an Entra identity that has been granted a
user. Password authentication would make this rule dangerous.

---

## Adding a developer, afterwards

```bash
az ad group member add --group ttc-developers \
  --member-id $(az ad user show --id nueva.persona@dominio.com --query id -o tsv)

az sql server firewall-rule create --resource-group rg-tacotuesday --server tacotuesday-sql \
  --name dev-nueva --start-ip-address <su ip> --end-ip-address <su ip>
```

Two commands. No password is created, sent, or stored. Removing someone is removing them from
the group — their access ends immediately, everywhere, with no key left behind.

---

## Removing SQL authentication entirely (recommended, later)

The `ttcadmin` SQL login still exists as a break-glass account. Once Entra access is confirmed
working for everybody, switch the server to Entra-only:

```bash
az sql server ad-only-auth enable --resource-group rg-tacotuesday --name tacotuesday-sql
```

After that, a leaked password is not a risk, because there is no password. Do this **after**
step 4 and 5 are verified — not before, or you lock yourself out.

---

## Troubleshooting: `/health/ready` returns 503 `Unhealthy`

Read the symptom precisely before changing anything. A body of `Unhealthy` means **ASP.NET
answered**: the container is running, ingress is fine, the image is fine. ASP.NET returns 503
for an unhealthy report, and the only check tagged `ready` that can fail is
`AddDbContextCheck<LeadsDbContext>(name: "leads-db")`, registered in `LeadsModule`. So the app
cannot open the database. Nothing else is broken.

(A 503 from Azure itself — no revision running, failing probes — has an Azure HTML body instead,
and `/health/live` fails too. Check `/health/live` first: if it returns 200, the problem is the
database, full stop.)

### 0. First: a changed secret does NOT reach a running replica

This is the trap that makes this bug look unfixable. `az containerapp secret set` stores the new
value and **does not restart anything**. The replicas that are already running keep the value they
were started with, so the log keeps printing the *old* error and it looks as though the new value
changed nothing.

Only a change to the container *template* creates a new revision. A secret is not part of the
template; an environment variable is. So either force a restart:

```bash
REV=$(az containerapp revision list -n tacotuesday-api -g rg-tacotuesday \
        --query "[?properties.active].name | [0]" -o tsv)
az containerapp revision restart -n tacotuesday-api -g rg-tacotuesday --revision $REV
```

…or avoid the whole class of problem by holding the connection string as an **environment
variable** (step 6). `az containerapp update --set-env-vars` is a template change: it creates a new
revision, and the new replicas start with the new value. Nothing to remember.

**The tripwire:** if the log still names the *same character index* after you changed the value,
the replica is not reading what you wrote. Restart it before you touch the string again.

### 1. See the actual exception

`AddDbContextCheck` reports only `Unhealthy` over HTTP; the exception goes to the log.

```bash
az containerapp logs show -n tacotuesday-api -g rg-tacotuesday --tail 200 --follow
```

Then curl `/health/ready` and watch what appears. Match the message:

| In the log | Cause | Fix |
|---|---|---|
| `Login failed for user '<token-identified principal>'` | The identity authenticated with Entra but has no user in the database | step 5 |
| `ManagedIdentityCredential authentication unavailable` | No identity attached to the app | step 6, `identity assign` |
| `Cannot open server ... requested by the login` / `not allowed to access` | Firewall | step 7 |
| `Connection Timeout Expired` after ~30 s | Database auto-paused (free-limit quota) or firewall silently dropping | portal: database status |
| `interactive authentication is not supported` / hangs | Connection string still says `Active Directory Interactive` | step 6 |

### 2. Confirm what the app is actually configured with

```bash
az containerapp show -n tacotuesday-api -g rg-tacotuesday \
  --query "properties.template.containers[0].env" -o table

az containerapp identity show -n tacotuesday-api -g rg-tacotuesday -o json
```

If `ConnectionStrings__Leads` shows a `secretRef` instead of a value, the value lives in a
secret — read it with `az containerapp secret show`. If `userAssignedIdentities` is empty,
step 6 was never run.

### 3. Confirm the grant exists in the database

Connected to **`ttc-sqldb-prod`** as the Entra admin:

```sql
SELECT name, type_desc, authentication_type_desc
FROM sys.database_principals
WHERE name = 'id-tacotuesday';
```

No row means step 5 was never run — which is the single most common reason for this 503. The
connection string is then correct and still cannot work: Entra issues the token, and SQL rejects
a principal it has never heard of.
