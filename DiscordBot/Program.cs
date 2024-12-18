using Discord;
using Discord.WebSocket;
using DiscordBot.Handler;
using DiscordBot.Handler.Interfaces;
using DiscordBot.Middleware;
using DiscordBot.Middleware.Interfaces;
using DiscordBot.Modules;
using DiscordBot.Modules.Interfaces;
using DiscordBot.Repositories;
using DiscordBot.Repositories.Interfaces;
using DiscordBot.Services;
using DiscordBot.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Services.AddLogging(config =>
{
    config.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.None);
    config.AddFilter("Microsoft", LogLevel.Warning);
});

builder.Services.AddSingleton<DiscordSocketClient>(provider =>
{
    var socketConfig = new DiscordSocketConfig
    {
        // Enabled all unprivileged intents except:
        // - GuildScheduledEvents
        // - GuildInvites
        GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildBans | GatewayIntents.GuildEmojis | GatewayIntents.GuildIntegrations |
                    GatewayIntents.GuildWebhooks | GatewayIntents.GuildVoiceStates | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions |
                    GatewayIntents.GuildMessageTyping | GatewayIntents.DirectMessages | GatewayIntents.DirectMessageReactions | GatewayIntents.DirectMessageTyping |
                    GatewayIntents.AutoModerationConfiguration | GatewayIntents.AutoModerationActionExecution | GatewayIntents.GuildMessagePolls |
                    GatewayIntents.DirectMessagePolls,
        MessageCacheSize = 10,
        AlwaysDownloadDefaultStickers = true,
        AlwaysResolveStickers = true,
        AlwaysDownloadUsers = false,
        AuditLogCacheSize = 10,
        LogLevel = LogSeverity.Warning

    };

    var client = new DiscordSocketClient(socketConfig);

    return client;
});

// Repositories
builder.Services.AddTransient<IConfigurationRepository, ConfigurationRepository>();

// Services
builder.Services.AddHostedService<DiscordClientService>(); // Add as a HostedService to run methods on app start
builder.Services.AddSingleton<IMusicService, MusicService>();
builder.Services.AddSingleton<IGuildService, GuildService>();

// Handlers
builder.Services.AddTransient<ISlashCommandHandler, SlashCommandHandler>();
builder.Services.AddTransient<IReactionHandler, ReactionHandler>();
builder.Services.AddTransient<IButtonHandler, ButtonHandler>();
builder.Services.AddTransient<IMessageHandler, MessageHandler>();
builder.Services.AddTransient<IGuildHandler, GuildHandler>();

// Middlewares
builder.Services.AddSingleton<IErrorHandlerMiddleware, ErrorHandlerMiddleware>();

// Modules
builder.Services.AddTransient<IYtDlp, YtDlp>();
builder.Services.AddTransient<IFFmpeg, FFmpeg>();

var app = builder.Build();

// Execute application wrapped with an error handler
var errorHandler = app.Services.GetRequiredService<IErrorHandlerMiddleware>();

await errorHandler.Execute(async () => await app.RunAsync());