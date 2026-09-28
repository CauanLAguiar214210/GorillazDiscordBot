FROM eclipse-temurin:17-jre AS base

FROM eclipse-temurin:17-jdk AS build

ARG LAVALINK_VERSION=4.2.2

WORKDIR /build

RUN apt-get update \
    && apt-get install -y --no-install-recommends wget ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && wget -q "https://github.com/lavalink-devs/Lavalink/releases/download/${LAVALINK_VERSION}/Lavalink.jar" -O Lavalink.jar

FROM base AS final

WORKDIR /opt/Lavalink

COPY --from=build /build/Lavalink.jar Lavalink.jar
COPY lavalink/application.yml application.yml
COPY GorillazDiscordBot.Api/Resources/Sounds/ sounds/

ENV JAVA_OPTS="-Xms128M -Xmx256M"

EXPOSE 2333

ENTRYPOINT ["java", "-jar", "Lavalink.jar"]