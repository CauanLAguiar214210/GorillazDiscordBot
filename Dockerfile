FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine AS base
RUN apk add --no-cache icu-data-full icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG NUGET_AUTH_TOKEN
WORKDIR /src

COPY GorillazDiscordBot.Api/GorillazDiscordBot.Api.csproj GorillazDiscordBot.Api/
COPY GorillazDiscordBot.Domain/GorillazDiscordBot.Domain.csproj GorillazDiscordBot.Domain/
COPY GorillazDiscordBot.Infra/GorillazDiscordBot.Infra.csproj GorillazDiscordBot.Infra/
COPY nuget.config ./

RUN if [ -n "$NUGET_AUTH_TOKEN" ]; then \
        dotnet nuget add source "https://nuget.pkg.github.com/CauanLAguiar214210/index.json" \
            --name github-luckymonkey \
            --username CauanLAguiar214210 \
            --password "$NUGET_AUTH_TOKEN" \
            --store-password-in-clear-text \
            --configfile /root/.nuget/NuGet/NuGet.Config; \
    fi \
    && dotnet restore "GorillazDiscordBot.Api/GorillazDiscordBot.Api.csproj"

COPY . .

RUN dotnet build "GorillazDiscordBot.Api/GorillazDiscordBot.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build \
    --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "GorillazDiscordBot.Api/GorillazDiscordBot.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GorillazDiscordBot.Api.dll"]
