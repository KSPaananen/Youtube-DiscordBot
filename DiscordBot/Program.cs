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

// Repositories
builder.Services.AddTransient<IConfigurationRepository, ConfigurationRepository>();

// Services
builder.Services.AddTransient<IMusicService, MusicService>();

// Handlers
builder.Services.AddTransient<ISlashCommandHandler, SlashCommandHandler>();
builder.Services.AddTransient<IReactionHandler, ReactionHandler>();
builder.Services.AddTransient<IUserHandler, UserHandler>();
builder.Services.AddTransient<IButtonHandler, ButtonHandler>();
builder.Services.AddTransient<IMessageHandler, MessageHandler>();

// Middlewares
builder.Services.AddSingleton<IErrorHandlerMiddleware, ErrorHandlerMiddleware>();

// Modules
builder.Services.AddTransient<IYtDlp, YtDlp>();
builder.Services.AddTransient<IFFmpeg, FFmpeg>();

builder.Services.AddHostedService<DiscordClientService>(); // Add as a HostedService to run methods on app start

var app = builder.Build();

// Execute application wrapped with an error handler
var errorHandler = app.Services.GetRequiredService<IErrorHandlerMiddleware>();

await errorHandler.Execute(async () => await app.RunAsync());