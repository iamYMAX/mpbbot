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
        Console.WriteLine("Application starting...");
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var appSettingsPath = Path.Combine(basePath, "appsettings.json");

        if (!System.IO.File.Exists(appSettingsPath))
        {
            Console.WriteLine("CRITICAL ERROR: appsettings.json not found!");
            return;
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var settings = configuration.Get<AppSettings>();

        if (settings == null)
        {
            Console.WriteLine("CRITICAL ERROR: Could not read settings from appsettings.json.");
            return;
        }
        
        if (string.IsNullOrEmpty(settings.Telegram.BotToken) || settings.Telegram.BotToken == "<TOKEN>")
        {
            Console.WriteLine("CRITICAL ERROR: Telegram BotToken is invalid or not configured.");
            return;
        }

        // Initialize services
        GigaChatService.Initialize(settings.GigaChat);
        var yandexSttService = new YandexSttService(settings.YandexSpeechKit);
        var mailReaderService = new MailReaderService(settings.EmailSettings);
        var mailAnalyzerService = new MailAnalyzerService();
        var mailReplyService = new MailReplyService(settings.EmailSettings);

        var botClient = new TelegramBotClient(settings.Telegram.BotToken);

        using var cts = new CancellationTokenSource();

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
        };

        var updateHandler = new UpdateHandler(yandexSttService, mailReaderService, mailAnalyzerService, mailReplyService);

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
