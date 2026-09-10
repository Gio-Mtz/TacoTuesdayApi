# Taco Tuesday — API

The backend for Taco Tuesday Consulting. A **modular monolith** in .NET 10.
The Angular frontend lives in a separate repository: [TacoTuesdayUI](https://github.com/Gio-Mtz/TacoTuesdayUI).

## Docker

### Build the image
```bash
docker build -t tacotuesday-api .
```

### Run the container
```bash
docker run -p 8080:8080 tacotuesday-api
```

The API will be available at `http://localhost:8080`.

### Run with env file (for settings)
```bash
docker run -p 8080:8080 --env-file .env.local tacotuesday-api
```

## Run it

```bash
dotnet restore
dotnet run --project src/TacoTuesday.Api
```

- API docs (Scalar): https://localhost:7001/scalar
- Liveness: https://localhost:7001/health/live
- Readiness: https://localhost:7001/health/ready

## Test it

```bash
dotnet test
```

## Structure

| Path                             | What                                                                   |
| -------------------------------- | ---------------------------------------------------------------------- |
| `src/TacoTuesday.Api`            | Host. DI, middleware, endpoint mapping. Thin.                          |
| `src/TacoTuesday.SharedKernel`   | `Result<T>`, `IClock`, `IEndpoint`, DI conventions. No business logic. |
| `src/TacoTuesday.Infrastructure` | Anything that talks to the outside world.                              |
| `src/Modules/*`                  | One folder per bounded context. Feature-sliced.                        |
| `tests/*`                        | Unit, integration (Testcontainers), architecture.                      |

## Rules

1. A module may reference another module's `Contracts` namespace. Nothing else.
2. Handlers never touch `HttpContext`.
3. Nothing references `TacoTuesday.Api`.
4. `dotnet test` enforces 1–3. If it's red, the rule was broken.

Decisions live in [`docs/adr`](docs/adr) — in the repo, so a reader on GitHub can see
why things are the way they are without access to anything else.
