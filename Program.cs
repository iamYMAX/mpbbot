using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using TelegramGigaChatBot.Configuration;
using TelegramGigaChatBot.Handlers;
using TelegramGigaChatBot.Services;

namespace TelegramGigaChatBot;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        await host.RunAsync();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                config.SetBasePath(basePath)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton(provider =>
                {
                    var configuration = provider.GetRequiredService<IConfiguration>();
                    var settings = configuration.Get<AppSettings>();
                    if (settings == null)
                    {
                        throw new InvalidOperationException("Could not read settings from appsettings.json.");
                    }
                    if (string.IsNullOrEmpty(settings.Telegram.BotToken) || settings.Telegram.BotToken == "<TOKEN>")
                    {
                        throw new InvalidOperationException("Telegram BotToken is invalid or not configured.");
                    }
                    return settings;
                });

                services.AddSingleton<ITelegramBotClient>(provider =>
                {
                    var settings = provider.GetRequiredService<AppSettings>();
                    return new TelegramBotClient(settings.Telegram.BotToken);
                });

                services.AddSingleton<GigaChatService>();
                services.AddSingleton<YandexSttService>(provider =>
                    new YandexSttService(provider.GetRequiredService<AppSettings>(), provider.GetRequiredService<ILogger<YandexSttService>>()));
                services.AddSingleton<MailReaderService>(provider =>
                    new MailReaderService(provider.GetRequiredService<UserEmailAccountService>(), provider.GetRequiredService<ILogger<MailReaderService>>()));
                services.AddSingleton<MailAnalyzerService>(provider =>
                    new MailAnalyzerService(provider.GetRequiredService<GigaChatService>(), provider.GetRequiredService<ILogger<MailAnalyzerService>>()));
                services.AddSingleton<MailReplyService>(provider =>
                    new MailReplyService(provider.GetRequiredService<AppSettings>(), provider.GetRequiredService<ILogger<MailReplyService>>(), provider.GetRequiredService<GigaChatService>()));
                services.AddSingleton<EmailCacheService>();
                services.AddSingleton<IUpdateHandler, UpdateHandler>(provider =>
                    new UpdateHandler(
                        provider.GetRequiredService<YandexSttService>(),
                        provider.GetRequiredService<MailReplyService>(),
                        provider.GetRequiredService<EmailCacheService>(),
                        provider.GetRequiredService<ILogger<UpdateHandler>>(),
                        provider.GetRequiredService<UserActionStateService>(),
                        provider.GetRequiredService<GigaChatService>(),
                        provider.GetRequiredService<UserEmailAccountService>()));

                services.AddSingleton<UserActionStateService>();
                services.AddSingleton<UserEmailAccountService>();

                services.AddHostedService<BackgroundEmailService>(provider =>
                    new BackgroundEmailService(
                        provider.GetRequiredService<ILogger<BackgroundEmailService>>(),
                        provider.GetRequiredService<MailReaderService>(),
                        provider.GetRequiredService<MailAnalyzerService>(),
                        provider.GetRequiredService<EmailCacheService>(),
                        provider.GetRequiredService<UserEmailAccountService>()));
                services.AddHostedService<BotInitializationService>();

                services.AddLogging();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            });
}
