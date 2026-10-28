FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore before copying sources so a code change does not re-download packages.
COPY Directory.Build.props Directory.Packages.props Gallery.slnx ./
COPY src/common/Gallery.Policy/Gallery.Policy.csproj src/common/Gallery.Policy/
COPY src/contracts/Gallery.Contracts/Gallery.Contracts.csproj src/contracts/Gallery.Contracts/
COPY src/gallery/Gallery.Domain/Gallery.Domain.csproj src/gallery/Gallery.Domain/
COPY src/gallery/Gallery.Application/Gallery.Application.csproj src/gallery/Gallery.Application/
COPY src/gallery/Gallery.Infrastructure/Gallery.Infrastructure.csproj src/gallery/Gallery.Infrastructure/
COPY src/gallery/Gallery.Api/Gallery.Api.csproj src/gallery/Gallery.Api/
COPY src/worker/Gallery.Worker.Application/Gallery.Worker.Application.csproj src/worker/Gallery.Worker.Application/
COPY src/worker/Gallery.Worker.Infrastructure/Gallery.Worker.Infrastructure.csproj src/worker/Gallery.Worker.Infrastructure/
COPY src/worker/Gallery.Worker/Gallery.Worker.csproj src/worker/Gallery.Worker/
COPY src/notifier/Gallery.Notifier/Gallery.Notifier.csproj src/notifier/Gallery.Notifier/
RUN dotnet restore src/gallery/Gallery.Api/Gallery.Api.csproj \
    && dotnet restore src/worker/Gallery.Worker/Gallery.Worker.csproj \
    && dotnet restore src/notifier/Gallery.Notifier/Gallery.Notifier.csproj

COPY . .
RUN dotnet publish src/gallery/Gallery.Api/Gallery.Api.csproj -c Release -o /app --no-restore -p:UseAppHost=false \
    && dotnet publish src/worker/Gallery.Worker/Gallery.Worker.csproj -c Release -o /worker --no-restore -p:UseAppHost=false \
    && dotnet publish src/notifier/Gallery.Notifier/Gallery.Notifier.csproj -c Release -o /notifier --no-restore -p:UseAppHost=false

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
# One image, three entry points: the worker and notifier Deployments override the
# command with `dotnet /worker/Gallery.Worker.dll` and `dotnet /notifier/Gallery.Notifier.dll`.
COPY --from=build /worker /worker
COPY --from=build /notifier /notifier

# Magick writes its pixel cache to disk; a read-only root filesystem needs this.
ENV TMPDIR=/tmp/gallery \
    MAGICK_TEMPORARY_PATH=/tmp/gallery \
    ASPNETCORE_URLS=http://+:8080

USER 1000
EXPOSE 8080
ENTRYPOINT ["dotnet", "Gallery.Api.dll"]
