# Base runtime image used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

# Install dependencies, opus & libsodium for discord voice functionality, FFmpeg for media handling, and Python tools
USER root
RUN apt-get update && apt-get install -y \
    libopus-dev \
    libsodium-dev \
    ffmpeg \
    python3 \
    python3-pip \
    python3-dev \
    build-essential \
    curl \
    python3-venv && \
    apt-get clean

# Create a virtual environment and install yt-dlp
RUN python3 -m venv /venv && \
    /venv/bin/pip install --upgrade pip && \
    /venv/bin/pip install yt-dlp

# Use the virtual environment for running the app
ENV PATH="/venv/bin:$PATH"

USER app

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DiscordBot/DiscordBot.csproj", "DiscordBot/"]
RUN dotnet restore "DiscordBot/DiscordBot.csproj"
COPY . . 
WORKDIR "/src/DiscordBot"
RUN dotnet build "./DiscordBot.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DiscordBot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DiscordBot.dll"]
