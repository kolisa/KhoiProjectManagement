# Build context is the repo root (see docker-compose.yml's `build.context: .`), not this folder -
# the API references sibling projects (Domain/Application/Infrastructure/Quartz), so Docker needs the
# whole solution tree available to restore/build them.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy just the .csproj files (plus the central-package-management props they all resolve against)
# first, so `dotnet restore` is its own cached layer - it only re-runs when a project actually changes
# its package references, not on every source-code edit.
COPY Directory.Build.props Directory.Packages.props ./
COPY KhoiProjectManagement.Domain/KhoiProjectManagement.Domain.csproj KhoiProjectManagement.Domain/
COPY KhoiProjectManagement.Application/KhoiProjectManagement.Application.csproj KhoiProjectManagement.Application/
COPY KhoiProjectManagement.Infrastructure/KhoiProjectManagement.Infrastructure.csproj KhoiProjectManagement.Infrastructure/
COPY KhoiProjectManagement.Quartz/KhoiProjectManagement.Quartz.csproj KhoiProjectManagement.Quartz/
COPY KhoiProjectManagementApi/KhoiProjectManagementApi.csproj KhoiProjectManagementApi/
RUN dotnet restore KhoiProjectManagementApi/KhoiProjectManagementApi.csproj

# Now bring in the actual source and publish.
COPY KhoiProjectManagement.Domain/ KhoiProjectManagement.Domain/
COPY KhoiProjectManagement.Application/ KhoiProjectManagement.Application/
COPY KhoiProjectManagement.Infrastructure/ KhoiProjectManagement.Infrastructure/
COPY KhoiProjectManagement.Quartz/ KhoiProjectManagement.Quartz/
COPY KhoiProjectManagementApi/ KhoiProjectManagementApi/
RUN dotnet publish KhoiProjectManagementApi/KhoiProjectManagementApi.csproj \
    -c Release -o /app/publish --no-restore

# Runtime image - the SDK layer above (~1GB+) never ships, only the published output does.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Npgsql probes for libgssapi_krb5 (GSSAPI/Kerberos auth negotiation) on every connection attempt and
# logs a scary-looking "cannot open shared object file" error when it's missing - harmless for the
# username/password auth this app actually uses, but confusing in the logs and easy to install properly.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# wwwroot/uploads and Logs are written to at runtime (uploaded attachments, invoices, wiki/idea
# files, daily log files) - see docker-compose.yml, which mounts these as named volumes onto these
# same paths so that content survives `docker compose down`/image rebuilds instead of living only in
# the container's writable layer.
RUN mkdir -p wwwroot/uploads Logs

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "KhoiProjectManagementApi.dll"]
