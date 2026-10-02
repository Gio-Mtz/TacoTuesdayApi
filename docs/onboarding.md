# Onboarding — de cero a corriendo

**Meta: 15 minutos.** Si te toma más, es un bug de este documento. Dime dónde te atoraste.

No instalas SQL Server. No levantas contenedores. No creas ninguna contraseña.
**No vas a tocar un solo secreto en todo este documento.**

---

## 0. Lo que necesitas instalado

| Herramienta | Versión | Comprobar |
| --- | --- | --- |
| .NET SDK | 10.0.x (lo fija `global.json`) | `dotnet --list-sdks` |
| Node.js | 22 LTS | `node --version` |
| Azure CLI | cualquiera reciente | `az version` |
| Git | cualquiera reciente | `git --version` |

Nada de Docker. Nada de SQL Server local.

## 1. Clona los dos repos

```bash
git clone https://github.com/Gio-Mtz/TacoTuesdayApi.git
git clone https://github.com/Gio-Mtz/TacoTuesdayUI.git
```

## 2. Inicia sesión en Azure

```bash
az login
```

Se abre el navegador, entras con tu cuenta de trabajo, y ya.

**Eso es todo lo que hay que hacer para tener acceso a la base de datos.** No hay contraseña
que pedirle a nadie, ni cadena de conexión que configurar, ni archivo `.env` que llenar.

> ¿Por qué funciona? La cadena de conexión dice `Authentication=Active Directory Default`.
> El cliente de SQL busca una identidad de Azure en tu máquina — tu sesión de `az login`, la de
> Visual Studio o la de VS Code — y pide un token con ella. **No existe ninguna contraseña**,
> ni en tu máquina ni en el repo ni en Azure. Por eso la cadena de conexión está commiteada en
> `appsettings.Development.json`: no hay nada que esconder.
>
> Si te sale `Login failed for user '<token-identified principal>'`, significa que tu cuenta
> aún no está en el grupo `ttc-developers`. Pídeselo a Gio — es un clic.

## 3. Corre la API

```bash
cd TacoTuesdayApi
dotnet run --project src/TacoTuesday.Api
```

| Revisa | Esperas |
| --- | --- |
| `https://localhost:7001/health/live` | `Healthy` — el proceso está arriba |
| `https://localhost:7001/health/ready` | `Healthy` — **la base contestó** |
| `https://localhost:7001/scalar` | La referencia de la API |

Si `ready` da 503, la API está corriendo pero no alcanza la base. Casi siempre es una de dos:
no hiciste `az login`, o tu IP no está en el firewall (ver abajo).

## 4. Corre la UI

En otra terminal, en el otro repo:

```bash
npm ci
npm start
```

Abre `http://localhost:4200`.

## 5. Compruébalo de punta a punta

Llena el formulario de la lista de espera con tu propio correo. Luego, en Azure Data Studio o
en el portal (Query editor):

```sql
SELECT TOP 10 Email, Kind, CreatedAtUtc FROM Leads ORDER BY CreatedAtUtc DESC;
```

Tu correo debe estar ahí. **Eso es "mi entorno funciona".**

---

## Lo único que tienes que entender sobre las bases

Hay **dos**, en el mismo servidor:

| Base | Quién la usa | Qué hay dentro |
| --- | --- | --- |
| `ttc-sqldb-dev` | **Tú, en local.** Y el CI | Datos de prueba. Rómpela sin miedo |
| `ttc-sqldb-prod` | **Solo** la Container App en Azure | Gente real. Nadie se conecta desde su máquina |

Tu máquina apunta a **dev**, siempre. No hay configuración que cambiar: es lo que dice
`appsettings.Development.json`, y `Development` es el entorno por defecto de `dotnet run`.

**Nadie se conecta a prod desde su laptop.** No porque esté prohibido por política, sino porque
sería compartir la base con los usuarios reales: tus pruebas quedarían entre sus registros, un
`dotnet ef database update` tuyo cambiaría el esquema de producción, y el consumo de tu laptop
se comería la cuota gratuita mensual — que al agotarse **pausa la base**, o sea tira el sitio.

---

## Comandos del día a día

```bash
dotnet run --project src/TacoTuesday.Api    # API   (terminal 1)
npm start                                    # UI    (terminal 2, otro repo)
dotnet test                                  # toda la suite
az login                                     # solo si el token expiró
```

El token de Azure dura unas horas. Si de repente la API deja de alcanzar la base después de un
rato, `az login` otra vez y listo.

---

## Si algo no funciona

| Síntoma | Causa |
| --- | --- |
| `Login failed for user '<token-identified principal>'` | Tu cuenta no está en `ttc-developers`. Pídeselo a Gio |
| `Cannot open server ... requested by the login` | Tu IP no está en el firewall. Pídeselo a Gio, o ábrelo tú con el comando de abajo |
| `/health/ready` da 503 | Falta `az login`, o lo de arriba |
| La primera petición tarda varios segundos | La base es *serverless* y estaba dormida. Es normal, despierta sola |
| `ng build` falla después de un pull | `npm ci` — alguien cambió una dependencia |

Si tienes permisos en la suscripción, puedes abrir tu propia IP:

```bash
az sql server firewall-rule create \
  --resource-group rg-tacotuesday --server tacotuesday-sql \
  --name "dev-$(whoami)" \
  --start-ip-address $(curl -s ifconfig.me) --end-ip-address $(curl -s ifconfig.me)
```

---

## Migraciones

No las corras contra prod desde tu máquina. Para la base de dev está bien y es parte del
trabajo normal:

```bash
dotnet ef database update \
  --project src/Migrations/TacoTuesday.Migrations.SqlServer \
  --startup-project src/TacoTuesday.Api
```

Cómo se crean, y cómo llegan a producción, está en [`database.md`](database.md).
