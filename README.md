# Youtube-DiscordBot
A simple-ish discord music bot with youtube support.

## Libraries used
- [Discord.NET](https://github.com/discord-net/Discord.Net)

## Integrated Tools
- [FFmpeg](https://github.com/FFmpeg/FFmpeg) for stream & audio processing
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) for youtube support

## Features
- Supports search queries, direct links to videos and links to playlists/mixes/radios
- Control the bot with slashcommands & buttons
- Queue system

## Slashcommands
- /play
- /skip-song
- /stop-playing
- /clear-queue

## Running the bot with command prompt
For optimal performance it's recommended to run Docker on a Linux-based operating system as the host OS. Windows is fine, but expect reduced performance as the number of servers increases.

1. Download & install [Docker](https://www.docker.com/)
   - Verify dockers installation by running `docker --version`

3. Pull the latest image from GitHub's Container registry
   - Run `docker pull ghcr.io/kspaananen/youtube-discordbot:latest`
   - Verify you were able to pull the image with `docker images`

4. Creating a new container and running the image inside it
   - Run `docker run -e Bot__Token=YourBotToken --name YourContainerName ghcr.io/kspaananen/youtube-discordbot:latest`
     - Replace `YourBotToken` with your bot token & `YourContainerName` with a proper container name
   - To stop the container you can either:
     - Press `ctrl + c`
     - Run `docker stop YourContainerName`

5. Running the container again after creation
   - Run `docker start YourContainerName`
     - Use `docker ps -a` to see all created containers

## Q&A
### Do i have to use command prompt?
After pulling the latest image with command prompt you can use Docker desktop to handle containers and images with a graphical user interface. Just make sure to include enviromental variable Bot__Token with your bot token under 'Optional settings' when running images in a new container.

### How can i change the bot token?
To host another bot you need to create a new container with the same image. Repeat step 3, but pass a different token and container name.

### Docker commands you may need with this application
- `docker ps -a` to see all containers
- `docker images` to see all images
- `docker run -e Bot__Token=YourBotToken --name YourContainerName imagename:tag` to create a new container
- `docker start YourContainerName` to start an existing container
- `docker restart YourContainerName` to restart a running container
- `docker stop YourContainerName` to stop a running container
- `docker rm YourContainerName` to remove a container
