﻿using Discord.WebSocket;

namespace DiscordBot.Services.Interfaces
{
    public interface IMessageService
    {
        Task SendJoinedGuildMessage(SocketGuild guild);

    }
}
