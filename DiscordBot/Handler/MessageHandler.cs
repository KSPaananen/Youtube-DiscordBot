﻿using Discord;
using Discord.WebSocket;
using DiscordBot.Handler.Interfaces;

namespace DiscordBot.Handler
{
    public class MessageHandler : IMessageHandler
    {
        public MessageHandler()
        {

        }

        public Task HandleMessageCommandExecutedAsync(SocketMessageCommand command)
        {
            return Task.CompletedTask;
        }

        public Task HandleMessageDeletedAsync(Cacheable<IMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> cachedChannel)
        {
            return Task.CompletedTask;
        }

        public Task HandleMessageReceivedAsync(SocketMessage message)
        {
            return Task.CompletedTask;
        }

        public Task HandleMessageUpdatedAsync(Cacheable<IMessage, ulong> cachedMessage, SocketMessage message, ISocketMessageChannel channel)
        {
            return Task.CompletedTask;
        }


    }
}
