FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore before copying sources so a code change does not re-download packages.
COPY Directory.Build.props Directory.Packages.props Gallery.slnx ./
COPY src/core/Gallery.Domain/Gallery.Domain.csproj src/core/Gallery.Domain/
COPY src/core/Gallery.Contracts/Gallery.Contracts.csproj src/core/Gallery.Contracts/
COPY src/platform/Gallery.Persistence/Gallery.Persistence.csproj src/platform/Gallery.Persistence/
COPY src/core/Gallery.Application/Gallery.Application.csproj src/core/Gallery.Application/
COPY src/platform/Gallery.Infrastructure/Gallery.Infrastructure.csproj src/platform/Gallery.Infrastructure/
COPY src/hosts/Gallery.Api/Gallery.Api.csproj src/hosts/Gallery.Api/
COPY src/hosts/Gallery.Worker/Gallery.Worker.csproj src/hosts/Gallery.Worker/
RUN dotnet restore src/hosts/Gallery.Api/Gallery.Api.csproj \
    && dotnet restore src/hosts/Gallery.Worker/Gallery.Worker.csproj

COPY . .
RUN dotnet publish src/hosts/Gallery.Api/Gallery.Api.csproj -c Release -o /app --no-restore -p:UseAppHost=false \
    && dotnet publish src/hosts/Gallery.Worker/Gallery.Worker.csproj -c Release -o /worker --no-restore -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# curl serves the healthcheck; libgomp1 is what Magick.NET's native library links against.
# UID/GID 1000 already exists in this base image, which is what the chart's
# securityContext expects, so the container simply runs as that user.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl libgomp1 \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /tmp/gallery \
    && chown 1000:1000 /tmp/gallery

WORKDIR /app
COPY --from=build /app .
# One image, two entry points: the worker Deployment overrides the command with
# `dotnet /worker/Gallery.Worker.dll`.
COPY --from=build /worker /worker

# Magick writes its pixel cache to disk; a read-only root filesystem needs this.
ENV TMPDIR=/tmp/gallery \
    MAGICK_TEMPORARY_PATH=/tmp/gallery \
    ASPNETCORE_URLS=http://+:8080

USER 1000
EXPOSE 8080
ENTRYPOINT ["dotnet", "Gallery.Api.dll"]
