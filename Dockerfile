FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine AS base
RUN apk add --no-cache icu-data-full icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY GorillazDiscordBot.Api/GorillazDiscordBot.Api.csproj GorillazDiscordBot.Api/
COPY GorillazDiscordBot.Domain/GorillazDiscordBot.Domain.csproj GorillazDiscordBot.Domain/
COPY GorillazDiscordBot.Infra/GorillazDiscordBot.Infra.csproj GorillazDiscordBot.Infra/

RUN dotnet restore "GorillazDiscordBot.Api/GorillazDiscordBot.Api.csproj"

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
