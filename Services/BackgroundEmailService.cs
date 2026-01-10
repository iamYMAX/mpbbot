using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramGigaChatBot.Models;
using Microsoft.Extensions.Logging;

namespace TelegramGigaChatBot.Services
{
    public class BackgroundEmailService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly MailReaderService _mailReaderService;
        private readonly MailAnalyzerService _mailAnalyzerService;
        private readonly EmailCacheService _emailCacheService;
        private readonly long _notificationChatId;
        private readonly int _checkIntervalMinutes;

        public BackgroundEmailService(
            ITelegramBotClient botClient,
            MailReaderService mailReaderService,
            MailAnalyzerService mailAnalyzerService,
            EmailCacheService emailCacheService,
            long notificationChatId,
            int checkIntervalMinutes)
        {
            _botClient = botClient;
            _mailReaderService = mailReaderService;
            _mailAnalyzerService = mailAnalyzerService;
            _emailCacheService = emailCacheService;
            _notificationChatId = notificationChatId;
            _checkIntervalMinutes = checkIntervalMinutes;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            LoggingService.Logger?.LogInformation("Background email service started.");
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndProcessEmails(cancellationToken);
                }
                catch (Exception ex)
                {
                    LoggingService.Logger?.LogError(ex, "Error in background email checking loop.");
                }

                await Task.Delay(TimeSpan.FromMinutes(_checkIntervalMinutes), cancellationToken);
            }
        }

        private async Task CheckAndProcessEmails(CancellationToken cancellationToken)
        {
            var unreadEmails = await _mailReaderService.GetUnreadEmailsAsync(cancellationToken);
            if (!unreadEmails.Any())
            {
                return;
            }

            LoggingService.Logger?.LogInformation($"Found {unreadEmails.Count} new emails. Starting analysis...");

            foreach (var email in unreadEmails)
            {
                var analyzedEmail = await _mailAnalyzerService.AnalyzeEmailAsync(email, cancellationToken);
                var messageId = analyzedEmail.OriginalMessage.MessageId;
                _emailCacheService.Add(messageId, analyzedEmail);

                var messageText = $@"
📬 *New Email*
*From:* {analyzedEmail.OriginalMessage.From}
*Subject:* {analyzedEmail.OriginalMessage.Subject}
*Priority:* {analyzedEmail.Priority}

*Summary:*
{analyzedEmail.Summary}

*Action Required:*
{analyzedEmail.ActionRequired}";

                var emailKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new [] {
                        InlineKeyboardButton.WithCallbackData("✍️ Generate Reply", $"generate_reply_{messageId}"),
                        InlineKeyboardButton.WithCallbackData("🎙️ Voice Reply", $"voice_reply_{messageId}")
                    },
                    new [] {
                        InlineKeyboardButton.WithCallbackData("📝 Manual Reply", $"manual_reply_{messageId}"),
                        InlineKeyboardButton.WithCallbackData("🔍 View Analysis", $"view_analysis_{messageId}")
                    },
                    new [] {
                        InlineKeyboardButton.WithCallbackData("🗑 Ignore", $"ignore_email_{messageId}")
                    },
                });

                await _botClient.SendMessageAsync(
                    chatId: _notificationChatId,
                    text: messageText,
                    replyMarkup: emailKeyboard,
                    cancellationToken: cancellationToken);
            }
        }
    }
}
