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

### 6. Point the Container App at prod, without a password

```bash
az containerapp update --name tacotuesday-api --resource-group rg-tacotuesday \
  --set-env-vars "ConnectionStrings__Leads=Server=tcp:tacotuesday-sql.database.windows.net,1433;Initial Catalog=ttc-sqldb-prod;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30"
```

Note this is an **environment variable, not a secret** — because it contains no secret.

Also make sure the Container App has the user-assigned identity attached and that
`AZURE_CLIENT_ID` is set to that identity's client id, so `Active Directory Default` picks the
right one when more than one identity is present:

```bash
az containerapp update --name tacotuesday-api --resource-group rg-tacotuesday \
  --set-env-vars "AZURE_CLIENT_ID=$(az identity show -n id-tacotuesday -g rg-tacotuesday --query clientId -o tsv)"
```

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
