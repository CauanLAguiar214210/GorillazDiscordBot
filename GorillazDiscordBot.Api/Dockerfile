# Runtime final
FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine AS base
RUN apk add --no-cache icu-data-full icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app

# Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copia os csproj para aproveitar cache do restore
COPY GorillazDiscordBot/GorillazDiscordBot.Api.csproj GorillazDiscordBot/
COPY GorillazDiscordBot.Domain/GorillazDiscordBot.Domain.csproj GorillazDiscordBot.Domain/
COPY GorillazDiscordBot.Infra/GorillazDiscordBot.Infra.csproj GorillazDiscordBot.Infra/

RUN dotnet restore "GorillazDiscordBot/GorillazDiscordBot.Api.csproj"

# Copia o restante do código
COPY . .

RUN dotnet build "GorillazDiscordBot/GorillazDiscordBot.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build \
    --no-restore

# Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "GorillazDiscordBot/GorillazDiscordBot.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# Imagem final
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GorillazDiscordBot.Api.dll"]
