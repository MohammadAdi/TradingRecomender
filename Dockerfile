# ============================================================================
# Stage 1 — Build
# Uses the regular .NET SDK image (not Alpine — too slow for SDK / restore)
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy NuGet project files first — cached if no project changes
COPY TradingRecommender.sln ./
COPY TradingRecommender.Domain/TradingRecommender.Domain.csproj          TradingRecommender.Domain/
COPY TradingRecommender.Application/TradingRecommender.Application.csproj TradingRecommender.Application/
COPY TradingRecommender.Infrastructure/TradingRecommender.Infrastructure.csproj TradingRecommender.Infrastructure/
COPY TradingRecommender.Worker/TradingRecommender.Worker.csproj          TradingRecommender.Worker/

RUN dotnet restore TradingRecommender.Worker/TradingRecommender.Worker.csproj

# Now copy the rest of the source and publish
COPY . .
RUN dotnet publish TradingRecommender.Worker/TradingRecommender.Worker.csproj \
        -c Release \
        -o /app/publish \
        --no-restore \
        /p:UseAppHost=false

# ============================================================================
# Stage 2 — Runtime (Alpine, non-root, minimal attack surface)
# ============================================================================
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS runtime

# Install ICU + tzdata needed for System.Text.Json + TimeZoneInfo
# (Alpine's musl is missing these by default)
RUN apk add --no-cache icu-libs tzdata \
    && cp /usr/share/zoneinfo/Asia/Jakarta /etc/localtime \
    && echo "Asia/Jakarta" > /etc/timezone

# Run as non-root user (security hardening)
RUN addgroup -S app && adduser -S app -G app
USER app
WORKDIR /app

# Copy published binaries from build stage
COPY --from=build --chown=app:app /app/publish .

# Health-check via env var (optional)
ENV TZ=Asia/Jakarta \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_URLS=

# Quartz needs to bind a health/control port in some hosts; expose a wide range
EXPOSE 8080

ENTRYPOINT ["dotnet", "TradingRecommender.Worker.dll"]