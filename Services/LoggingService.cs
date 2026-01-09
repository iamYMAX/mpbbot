using Microsoft.Extensions.Logging;

namespace TelegramGigaChatBot.Services
{
    public static class LoggingService
    {
        public static ILogger? Logger { get; private set; }

        public static void Initialize()
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("TelegramGigaChatBot", LogLevel.Debug)
                    .AddConsole();
            });

            Logger = loggerFactory.CreateLogger("TelegramGigaChatBot");
        }
    }
}
