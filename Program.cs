using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TelegramGigaChatBot.Configuration;
using TelegramGigaChatBot.Handlers;
using TelegramGigaChatBot.Services;

namespace TelegramGigaChatBot;

public static class Program
{
    public static async Task Main(string[] args)
    {
        LoggingService.Initialize();
        LoggingService.Logger?.LogInformation("Application starting...");
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var appSettingsPath = Path.Combine(basePath, "appsettings.json");

        if (!System.IO.File.Exists(appSettingsPath))
        {
            LoggingService.Logger?.LogCritical("appsettings.json not found!");
            return;
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var settings = configuration.Get<AppSettings>();

        if (settings == null)
        {
            LoggingService.Logger?.LogCritical("Could not read settings from appsettings.json.");
            return;
        }
        
        if (string.IsNullOrEmpty(settings.Telegram.BotToken) || settings.Telegram.BotToken == "<TOKEN>")
        {
            LoggingService.Logger?.LogCritical("Telegram BotToken is invalid or not configured.");
            return;
        }

        // Initialize services
        GigaChatService.Initialize(settings.GigaChat);
        var yandexSttService = new YandexSttService(settings.YandexSpeechKit);
        var mailReaderService = new MailReaderService(settings.EmailSettings);
        var mailAnalyzerService = new MailAnalyzerService();
        var mailReplyService = new MailReplyService(settings.EmailSettings);
        var emailCacheService = new EmailCacheService();

        var botClient = new TelegramBotClient(settings.Telegram.BotToken);

        using var cts = new CancellationTokenSource();

        // Start background email checking
        if (settings.Telegram.NotificationChatId != 0)
        {
            var backgroundEmailService = new BackgroundEmailService(botClient, mailReaderService, mailAnalyzerService, emailCacheService, settings.Telegram.NotificationChatId, settings.BackgroundService.CheckIntervalMinutes);
            _ = backgroundEmailService.StartAsync(cts.Token);
        }
        else
        {
            Console.WriteLine("WARNING: Telegram.NotificationChatId is not set. Background email checking is disabled.");
        }

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
        };

        var updateHandler = new UpdateHandler(yandexSttService, mailReaderService, mailAnalyzerService, mailReplyService, emailCacheService);

        botClient.StartReceiving(
            updateHandler: updateHandler,
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token
        );

        var me = await botClient.GetMe(cancellationToken: cts.Token);
        Console.WriteLine($"Start listening for @{me.Username}");
        await Task.Delay(-1, cts.Token);
    }
}
