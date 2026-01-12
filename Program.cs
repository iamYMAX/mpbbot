using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var settings = configuration.Get<AppSettings>();

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });

        var botClient = new TelegramBotClient(settings.Telegram.BotToken);

        var gigaChatService = new GigaChatService(settings, loggerFactory.CreateLogger<GigaChatService>());
        var yandexSttService = new YandexSttService(settings, loggerFactory.CreateLogger<YandexSttService>());
        var userEmailAccountService = new UserEmailAccountService(settings);
        var mailReaderService = new MailReaderService(userEmailAccountService, loggerFactory.CreateLogger<MailReaderService>());
        var mailAnalyzerService = new MailAnalyzerService(gigaChatService, loggerFactory.CreateLogger<MailAnalyzerService>());
        var mailReplyService = new MailReplyService(settings, loggerFactory.CreateLogger<MailReplyService>(), gigaChatService);
        var emailCacheService = new EmailCacheService();
        var userActionStateService = new UserActionStateService();

        var updateHandler = new UpdateHandler(yandexSttService, mailReplyService, emailCacheService, loggerFactory.CreateLogger<UpdateHandler>(), userActionStateService, gigaChatService, userEmailAccountService);

        var backgroundEmailService = new BackgroundEmailService(loggerFactory.CreateLogger<BackgroundEmailService>(), mailReaderService, mailAnalyzerService, emailCacheService, userEmailAccountService);
        backgroundEmailService.Start();

        using var cts = new CancellationTokenSource();

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
        };

        botClient.StartReceiving(
            updateHandler: updateHandler,
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token
        );

        var me = await botClient.GetMeAsync(cancellationToken: cts.Token);
        Console.WriteLine($"Start listening for @{me.Username}");
        await Task.Delay(-1, cts.Token);
    }
}
