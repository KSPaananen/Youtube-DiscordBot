﻿using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Handler.Interfaces
{
    public interface IButtonHandler
    {
        Task HandleButtonExecutedAsync(SocketMessageComponent component);

    }
}
