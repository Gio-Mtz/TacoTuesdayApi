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
COPY global.json Directory.Build.props Directory.Packages.props ./

# Then ONLY the .csproj files, and restore.
# Deliberate: Docker caches per layer. Restoring before the source is copied
# means editing a .cs file does not re-download every NuGet package. Copy the
# source first and you pay a full restore on every single build.
#
# We restore the Api project, not the solution: it pulls exactly the graph the
# host needs (SharedKernel, Infrastructure, both modules) and skips the test
# projects, which have no business inside a runtime image.
COPY src/TacoTuesday.Api/TacoTuesday.Api.csproj                             src/TacoTuesday.Api/
COPY src/TacoTuesday.SharedKernel/TacoTuesday.SharedKernel.csproj           src/TacoTuesday.SharedKernel/
COPY src/TacoTuesday.Infrastructure/TacoTuesday.Infrastructure.csproj       src/TacoTuesday.Infrastructure/
COPY src/Modules/TacoTuesday.Modules.Candidates/TacoTuesday.Modules.Candidates.csproj  src/Modules/TacoTuesday.Modules.Candidates/
COPY src/Modules/TacoTuesday.Modules.Companies/TacoTuesday.Modules.Companies.csproj    src/Modules/TacoTuesday.Modules.Companies/

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
