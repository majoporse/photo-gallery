FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore before copying sources so a code change does not re-download packages.
COPY Directory.Build.props Directory.Packages.props Gallery.slnx ./
COPY src/Gallery.Domain/Gallery.Domain.csproj src/Gallery.Domain/
COPY src/Gallery.Application/Gallery.Application.csproj src/Gallery.Application/
COPY src/Gallery.Infrastructure/Gallery.Infrastructure.csproj src/Gallery.Infrastructure/
COPY src/Gallery.Api/Gallery.Api.csproj src/Gallery.Api/
COPY src/Gallery.Worker/Gallery.Worker.csproj src/Gallery.Worker/
RUN dotnet restore src/Gallery.Api/Gallery.Api.csproj \
    && dotnet restore src/Gallery.Worker/Gallery.Worker.csproj

COPY . .
RUN dotnet publish src/Gallery.Api/Gallery.Api.csproj -c Release -o /app --no-restore -p:UseAppHost=false \
    && dotnet publish src/Gallery.Worker/Gallery.Worker.csproj -c Release -o /worker --no-restore -p:UseAppHost=false

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
