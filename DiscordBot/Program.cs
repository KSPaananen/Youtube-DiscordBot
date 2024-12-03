using DiscordBot.Handler;
using DiscordBot.Handler.Interfaces;
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

// Add services
builder.Services.AddTransient<IConfigurationRepository, ConfigurationRepository>();
builder.Services.AddTransient<ICommandHandler, CommandHandler>();
builder.Services.AddTransient<IVoiceService, VoiceService>();
builder.Services.AddSingleton<IErrorHandler, ErrorHandler>();

// Add modules
builder.Services.AddTransient<IAudio, Audio>();

// Add DiscordService as a HostedService to run methods on startup
builder.Services.AddHostedService<DiscordClientService>();

var app = builder.Build();

// Execute application wrapped with an error handler
var errorHandler = app.Services.GetRequiredService<IErrorHandler>();

await errorHandler.Execute(async () => await app.RunAsync());