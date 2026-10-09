# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first (layer cache optimization)
COPY ELIBAPI.sln ./
COPY ELIBAPI.Core/ELIBAPI.Core.csproj             ELIBAPI.Core/
COPY ELIBAPI.Infrastructure/ELIBAPI.Infrastructure.csproj  ELIBAPI.Infrastructure/
COPY ELIBAPI.API/ELIBAPI.API.csproj               ELIBAPI.API/

# Restore NuGet packages
RUN dotnet restore ELIBAPI.API/ELIBAPI.API.csproj

# Copy all source code
COPY ELIBAPI.Core/             ELIBAPI.Core/
COPY ELIBAPI.Infrastructure/   ELIBAPI.Infrastructure/
COPY ELIBAPI.API/              ELIBAPI.API/

# Publish in Release mode
RUN dotnet publish ELIBAPI.API/ELIBAPI.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Font cho dấu tên người xem trên PDF tài liệu số (PdfWatermarkHelper) — DejaVu Sans có đủ dấu tiếng Việt.
RUN apt-get update \
    && apt-get install -y --no-install-recommends fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd --system appgroup && useradd --system --gid appgroup --no-create-home appuser

# Copy published output
COPY --from=build /app/publish .

# Set ownership
RUN chown -R appuser:appgroup /app
USER appuser

# Expose port
EXPOSE 8080

# Environment
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ELIBAPI.API.dll"]
