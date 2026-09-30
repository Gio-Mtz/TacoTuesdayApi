# =============================================================================
# Multi-stage build for TacoTuesday.Api
#
# The image that COMPILES is not the image that SHIPS. We build in one stage
# and copy only the published output into the final one, so what reaches Azure
# is runtime + your DLLs (~110 MB) instead of the whole SDK (~800 MB).
# =============================================================================

# ---- Stage 1: build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Build configuration files first. These change rarely, so this layer stays
# cached across almost every code change.
#
# `.editorconfig` belongs in this list and is not a formatting nicety here:
# Directory.Build.props turns on EnforceCodeStyleInBuild together with
# TreatWarningsAsErrors, so the analyser severities in that file are part of what
# compiles. Leave it out and the build fails on rules the repository has
# deliberately switched off -- CA1308 for the `ToLowerInvariant()` that produces
# `NormalizedEmail`, for one, which is suppressed on purpose and explained in
# CreateLeadHandler.cs. CI passes, the image does not, and the error blames the
# code instead of this file.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./

# Then ONLY the .csproj files, and restore.
# Deliberate: Docker caches per layer. Restoring before the source is copied
# means editing a .cs file does not re-download every NuGet package. Copy the
# source first and you pay a full restore on every single build.
#
# We restore the Api project, not the solution: it pulls exactly the graph the
# host needs (SharedKernel, Infrastructure, the three modules, the migrations) and
# skips the test projects, which have no business inside a runtime image.
#
# THE INVARIANT, and it has already been broken once: this list must name EVERY
# .csproj in the Api project's ProjectReference closure. `dotnet restore` walks
# those references and stops at the first one that is not in the build context:
#
#   MSB3202: The project file "/source/src/Modules/TacoTuesday.Modules.Leads/
#   TacoTuesday.Modules.Leads.csproj" was not found.
#
# That is what happened on 29-sep. US-004 added the Leads module and the Migrations
# project to the closure, this file was not touched, and every `docker build` and
# `az acr build` from that commit on died at restore. Nobody noticed because nothing
# in the repository built this file -- so the running revision kept serving the
# 09-sep image, `/health/live` and `/health/ready` answered Healthy from it, and
# `POST /api/leads` answered **404**: a symptom that reads exactly like a bug in an
# endpoint that had in fact never been shipped.
#
# The guard is the `container-image` job in .github/workflows/ci.yml, which builds
# this file on every push. Do not delete one without the other: an explicit list
# with nothing building it is a list that drifts in silence.
COPY src/TacoTuesday.Api/TacoTuesday.Api.csproj                             src/TacoTuesday.Api/
COPY src/TacoTuesday.SharedKernel/TacoTuesday.SharedKernel.csproj           src/TacoTuesday.SharedKernel/
COPY src/TacoTuesday.Infrastructure/TacoTuesday.Infrastructure.csproj       src/TacoTuesday.Infrastructure/
COPY src/Modules/TacoTuesday.Modules.Candidates/TacoTuesday.Modules.Candidates.csproj  src/Modules/TacoTuesday.Modules.Candidates/
COPY src/Modules/TacoTuesday.Modules.Companies/TacoTuesday.Modules.Companies.csproj    src/Modules/TacoTuesday.Modules.Companies/
COPY src/Modules/TacoTuesday.Modules.Leads/TacoTuesday.Modules.Leads.csproj            src/Modules/TacoTuesday.Modules.Leads/
COPY src/Migrations/TacoTuesday.Migrations.SqlServer/TacoTuesday.Migrations.SqlServer.csproj  src/Migrations/TacoTuesday.Migrations.SqlServer/

RUN dotnet restore src/TacoTuesday.Api/TacoTuesday.Api.csproj

# Now the source.
COPY src/ src/

# Release build => TreatWarningsAsErrors is on. A warning fails the image.
RUN dotnet publish src/TacoTuesday.Api/TacoTuesday.Api.csproj \
    -c Release -o /app --no-restore

# ---- Stage 2: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app ./
USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Globalization is on (Directory.Build.props sets InvariantGlobalization=false),
# so the ICU data in the aspnet image is required. Do not switch to the
# -alpine or -composite variants without checking that first.

ENTRYPOINT ["dotnet", "TacoTuesday.Api.dll"]
