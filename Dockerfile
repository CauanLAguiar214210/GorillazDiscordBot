# Runtime final
FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine AS base
WORKDIR /app

# Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src

# Copia apenas o csproj para aproveitar o cache do restore
COPY ["GorillazDiscordBot.csproj", "./"]

RUN dotnet restore "GorillazDiscordBot.csproj"

# Copia o restante do código
COPY . .

# Build sem restore duplicado
RUN dotnet build "GorillazDiscordBot.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build \
    --no-restore

# Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "GorillazDiscordBot.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# Imagem final
FROM base AS final

WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "GorillazDiscordBot.dll"]