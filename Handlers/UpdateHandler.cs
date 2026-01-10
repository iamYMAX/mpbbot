using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramGigaChatBot.Models;
using TelegramGigaChatBot.Services;
using System.IO;
using Microsoft.Extensions.Logging;

namespace TelegramGigaChatBot.Handlers;

public class UpdateHandler : IUpdateHandler
{
    // Note: In-memory state storage. Would need a persistent store for production.
    private static readonly Dictionary<long, string> UserEmailReplyState = new();
    private readonly YandexSttService _yandexSttService;
    private readonly MailReaderService _mailReaderService;
    private readonly MailAnalyzerService _mailAnalyzerService;
    private readonly MailReplyService _mailReplyService;
    private readonly EmailCacheService _emailCacheService;

    public UpdateHandler(YandexSttService yandexSttService, MailReaderService mailReaderService, MailAnalyzerService mailAnalyzerService, MailReplyService mailReplyService, EmailCacheService emailCacheService)
    {
        _yandexSttService = yandexSttService;
        _mailReaderService = mailReaderService;
        _mailAnalyzerService = mailAnalyzerService;
        _mailReplyService = mailReplyService;
        _emailCacheService = emailCacheService;
    }

    private const string DecisionCallback = "decision";
    private const string CancelCallback = "cancel";
    private const string ChangeModeCallback = "change_mode";
    private const string DeepAnalysisCallback = "deep_analysis";
    private const string DeepAnalysisRepeatCallback = "deep_analysis_repeat";
    private const string ChallengeCallback = "challenge";
    private const string ChallengeAnalysisCallback = "challenge_analysis";
    private const string FixDecisionCallback = "fix_decision";
    private const string StyleRationalCallback = "style_rational";
    private const string StyleHardCallback = "style_hard";
    private const string StyleProvocativeCallback = "style_provocative";
    private const string ClearContextCallback = "clear_context";
    private const string ConfirmClearContextCallback = "confirm_clear_context";
    private const string CancelClearCallback = "cancel_clear_context";
    private const string GenerateReplyCallback = "generate_reply";
    private const string VoiceReplyCallback = "voice_reply";
    private const string ManualReplyCallback = "manual_reply";
    private const string ViewAnalysisCallback = "view_analysis";
    private const string SendEmailCallback = "send_email";
    private const string EditReplyCallback = "edit_reply";
    private const string IgnoreEmailCallback = "ignore_email";
    private const string CheckEmailCallback = "check_email";
    
    public Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        var handler = update.Type switch
        {
            UpdateType.Message => HandleMessageAsync(botClient, update.Message!, cancellationToken),
            UpdateType.CallbackQuery => HandleCallbackQueryAsync(botClient, update.CallbackQuery!, cancellationToken),
            _ => Task.CompletedTask
        };

        return handler;
    }

    private async Task HandleMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (message.From is null) return;

        if (message.Voice is not null)
        {
            await HandleVoiceMessageAsync(botClient, message, cancellationToken);
            return;
        }

        if (message.Text is not { } messageText)
            return;
        
        var chatId = message.Chat.Id;
        var userId = message.From.Id;

        if (UserEmailReplyState.TryGetValue(userId, out var messageId))
        {
            // Handle manual email reply
            await ProcessManualEmailReply(botClient, message, chatId, messageId, cancellationToken);
            UserEmailReplyState.Remove(userId); // Reset state
            return;
        }

        var userIdString = userId.ToString();
        LoggingService.Logger?.LogInformation("Received a '{MessageText}' message in chat {ChatId} from user {UserIdString}.", messageText, chatId, userIdString);

        if (messageText.StartsWith("/start"))
        {
            const string startMessage = "Добро пожаловать! Я ваш управленческий советник.\n\n" +
                                        "1. Просто пишите мне свои мысли, идеи или описание ситуации.\n" +
                                        "2. После вашего сообщения появится кнопка '🧠 Получить решение'.\n" +
                                        "3. Нажмите её, чтобы я проанализировал контекст и дал совет.\n\n" +
                                        "Вы можете менять стиль моего мышления в меню после получения ответа.";
            await botClient.SendMessage(chatId: chatId, text: startMessage, cancellationToken: cancellationToken);
            return;
        }

        MemoryService.AddMessage(userId.ToString(), messageText);

        await botClient.SendMessage(
            chatId: chatId,
            text: "Принял. Что с этим сделать?",
            replyMarkup: GetInitialDecisionKeyboard(),
            cancellationToken: cancellationToken);
    }

    private async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is not { } callbackData || callbackQuery.From is null || callbackQuery.Message is null)
            return;

        var userId = callbackQuery.From.Id.ToString();
        var chatId = callbackQuery.Message.Chat.Id;

        // Acknowledge the callback query
        await botClient.AnswerCallbackQuery(callbackQueryId: callbackQuery.Id, cancellationToken: cancellationToken);

        // Remove the initial keyboard
        await botClient.EditMessageReplyMarkup(chatId: chatId, messageId: callbackQuery.Message.MessageId, cancellationToken: cancellationToken);

        if (callbackData.StartsWith(GenerateReplyCallback))
        {
            var parts = callbackData.Split('_');
            var messageId = parts[1];

            var email = _emailCacheService.Get(messageId);
            if (email != null)
            {
                if (parts.Length == 2) // Initial request, show style keyboard
                {
                    var styleKeyboard = GetReplyStyleKeyboard(messageId);
                    await botClient.SendMessage(chatId: chatId, text: "Выберите стиль ответа:", replyMarkup: styleKeyboard, cancellationToken: cancellationToken);
                }
                else // Style selected, generate draft
                {
                    var styleString = parts[2];
                    if (Enum.TryParse<ReplyStyle>(styleString, true, out var style))
                    {
                        await botClient.SendMessage(chatId: chatId, text: "✍️ Генерирую черновик ответа...", cancellationToken: cancellationToken);
                        var draft = await _mailReplyService.GenerateReplyDraftAsync(email, style, cancellationToken);

                        _emailCacheService.AddDraft(messageId, draft);
                        await botClient.SendMessage(chatId: chatId, text: $"Черновик:\n\n{draft}", replyMarkup: GetSendConfirmationKeyboard(messageId), cancellationToken: cancellationToken);
                    }
                }
            }
            return;
        }

        if (callbackData.StartsWith(VoiceReplyCallback))
        {
            var messageId = callbackData.Split('_')[1];
            var email = _emailCacheService.Get(messageId);
            if (email != null)
            {
                UserEmailReplyState[callbackQuery.From.Id] = messageId;
                await botClient.SendMessage(chatId: chatId, text: "🎙️ Пожалуйста, запишите ваш ответ.", cancellationToken: cancellationToken);
            }
            return;
        }

        if (callbackData.StartsWith(ManualReplyCallback))
        {
            var messageId = callbackData.Split('_')[1];
            var email = _emailCacheService.Get(messageId);
            if (email != null)
            {
                UserEmailReplyState[callbackQuery.From.Id] = messageId;
                await botClient.SendMessage(chatId: chatId, text: "📝 Введите ваш ответ:", cancellationToken: cancellationToken);
            }
            return;
        }

        if (callbackData.StartsWith(ViewAnalysisCallback))
        {
            var messageId = callbackData.Split('_')[1];
            var email = _emailCacheService.Get(messageId);
            if (email != null)
            {
                var analysisText = $@"
*Полный анализ письма:*
*От:* {email.OriginalMessage.From}
*Тема:* {email.OriginalMessage.Subject}
*Приоритет:* {email.Priority}
*Намерение:* {email.Intent}
*Эмоциональный тон:* {email.EmotionalTone}
*Ключевые данные:*
- Вопросы: {string.Join(", ", email.KeyData.GetValueOrDefault("questions", new List<string>()))}
- Требования: {string.Join(", ", email.KeyData.GetValueOrDefault("requirements", new List<string>()))}
- Дедлайны: {string.Join(", ", email.KeyData.GetValueOrDefault("deadlines", new List<string>()))}
- Деньги: {string.Join(", ", email.KeyData.GetValueOrDefault("money_mentions", new List<string>()))}
- Договоры: {string.Join(", ", email.KeyData.GetValueOrDefault("contract_mentions", new List<string>()))}
- Проблемы: {string.Join(", ", email.KeyData.GetValueOrDefault("problem_mentions", new List<string>()))}
*Резюме:* {email.Summary}
*Требуется действие:* {email.ActionRequired}
*Риск:* {email.Risk}";
                await botClient.SendMessage(chatId: chatId, text: analysisText, cancellationToken: cancellationToken);
            }
            return;
        }

        if (callbackData.StartsWith(IgnoreEmailCallback))
        {
            var messageId = callbackData.Split('_')[1];
            _emailCacheService.Remove(messageId);
            await botClient.SendMessage(chatId: chatId, text: "🗑 Письмо проигнорировано.", cancellationToken: cancellationToken);
            return;
        }
        
        if (callbackData.StartsWith("style_"))
        {
            var modeString = callbackData.Substring("style_".Length);
            if (ThinkingModeHelper.TryParse(modeString, out var newMode))
            {
                MemoryService.SetMode(userId, newMode);
                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"Принял. Стиль мышления: {ThinkingModeHelper.GetModeName(newMode)}.",
                    cancellationToken: cancellationToken);
            }
            return;
        }

        switch (callbackData)
        {
            case DecisionCallback:
            {
                var history = MemoryService.GetHistory(userId);
                if (!history.Any())
                {
                    await botClient.SendMessage(chatId: chatId, text: "Нет истории для анализа.", cancellationToken: cancellationToken);
                    return;
                }

                var lastMessage = history.Last();
                var mode = MemoryService.GetMode(userId);

                await botClient.SendChatAction(chatId: chatId, action: ChatAction.Typing, cancellationToken: cancellationToken);

                var response = await GigaChatService.GetDecisionAsync(lastMessage, history, mode, cancellationToken);

                MemoryService.SetLastSituation(userId, lastMessage);
                MemoryService.SetLastGigaChatResponse(userId, response);

                await botClient.SendMessage(chatId: chatId, text: response, cancellationToken: cancellationToken);
                await botClient.SendMessage(chatId: chatId, text: "Что дальше?", replyMarkup: GetFollowUpKeyboard(), cancellationToken: cancellationToken);
                break;
            }
            case ChangeModeCallback:
            {
                InlineKeyboardMarkup styleKeyboard = new(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("🧠 Рационально", StyleRationalCallback),
                        InlineKeyboardButton.WithCallbackData("🔨 Жёстко", StyleHardCallback),
                    },
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("🎭 Провокационно", StyleProvocativeCallback),
                    }
                });
                await botClient.SendMessage(chatId: chatId, text: "Как мне думать?", replyMarkup: styleKeyboard, cancellationToken: cancellationToken);
                break;
            }
            case CancelCallback:
                await botClient.SendMessage(chatId: chatId, text: "Действие отменено.", cancellationToken: cancellationToken);
                break;
            case CheckEmailCallback:
                await botClient.SendMessage(chatId: chatId, text: "⏳ Проверяю почту...", cancellationToken: cancellationToken);
                var unreadEmails = await _mailReaderService.GetUnreadEmailsAsync(cancellationToken);
                if (unreadEmails.Any())
                {
                    await botClient.SendMessage(chatId: chatId, text: $"📥 Найдено новых писем: {unreadEmails.Count}. Начинаю анализ...", cancellationToken: cancellationToken);
                    // This will trigger the background service to pick them up on its next run,
                    // so no need to process them here.
                }
                else
                {
                    await botClient.SendMessage(chatId: chatId, text: "👍 Новых писем нет.", cancellationToken: cancellationToken);
                }
                break;
            case DeepAnalysisCallback:
            case DeepAnalysisRepeatCallback:
            {
                var lastResponse = MemoryService.GetLastGigaChatResponse(userId);
                var lastSituation = MemoryService.GetLastSituation(userId);

                if (string.IsNullOrEmpty(lastResponse) || string.IsNullOrEmpty(lastSituation))
                {
                    await botClient.AnswerCallbackQuery(callbackQueryId: callbackQuery.Id, text: "Ошибка: нет данных для анализа.", showAlert: true, cancellationToken: cancellationToken);
                    return;
                }

                await botClient.SendMessage(chatId: chatId, text: "Анализирую глубже...", cancellationToken: cancellationToken);

                var history = MemoryService.GetHistory(userId);
                var mode = MemoryService.GetMode(userId);

                var deepAnalysis = await GigaChatService.GetDeepAnalysisAsync(lastResponse, lastSituation, history, mode, cancellationToken);

                MemoryService.SetLastGigaChatResponse(userId, deepAnalysis);

                await botClient.SendMessage(chatId: chatId, text: deepAnalysis, cancellationToken: cancellationToken);
                await botClient.SendMessage(chatId: chatId, text: "Что дальше?", replyMarkup: GetDeepAnalysisFollowUpKeyboard(), cancellationToken: cancellationToken);
                break;
            }
            case ChallengeCallback:
            case ChallengeAnalysisCallback:
            case FixDecisionCallback:
                await botClient.SendMessage(chatId: chatId, text: "Эта функция находится в разработке.", cancellationToken: cancellationToken);
                break;
            
            case ClearContextCallback:
            {
                InlineKeyboardMarkup confirmationKeyboard = new(new[]
                {
                    new [] { InlineKeyboardButton.WithCallbackData("✅ Очистить и начать заново", ConfirmClearContextCallback) },
                    new [] { InlineKeyboardButton.WithCallbackData("❌ Отмена", CancelClearCallback) },
                });
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "⚠️ Вы действительно хотите очистить контекст?\nИстория сообщений и предыдущие выводы будут удалены.",
                    replyMarkup: confirmationKeyboard,
                    cancellationToken: cancellationToken);
                break;
            }
            
            case ConfirmClearContextCallback:
            {
                MemoryService.ClearContext(userId);
                await botClient.SendMessage(chatId: chatId, text: "🧠 Контекст очищен.\nМожете описать новую ситуацию.", cancellationToken: cancellationToken);
                break;
            }

            case CancelClearCallback:
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Очистка отменена. Что дальше?",
                    replyMarkup: GetFollowUpKeyboard(),
                    cancellationToken: cancellationToken);
                break;
            }
            default:
                if (callbackData.StartsWith(SendEmailCallback))
                {
                    var messageId = callbackData.Split('_')[1];
                    var draft = _emailCacheService.GetDraft(messageId);
                    var email = _emailCacheService.Get(messageId);

                    if (draft != null && email != null)
                    {
                        var success = await _mailReplyService.SendReplyAsync(email.OriginalMessage.Account, email.OriginalMessage.From, $"Re: {email.OriginalMessage.Subject}", draft, cancellationToken);
                        if (success)
                        {
                            await botClient.SendMessage(chatId: chatId, text: "✅ Письмо успешно отправлено.", cancellationToken: cancellationToken);
                        }
                        else
                        {
                            await botClient.SendMessage(chatId: chatId, text: "❌ Не удалось отправить письмо.", cancellationToken: cancellationToken);
                        }
                        _emailCacheService.Remove(messageId);
                        _emailCacheService.RemoveDraft(messageId);
                    }
                }
                else if (callbackData.StartsWith(EditReplyCallback))
                {
                    var messageId = callbackData.Split('_')[1];
                    UserEmailReplyState[callbackQuery.From.Id] = messageId;
                    await botClient.SendMessage(chatId: chatId, text: "✏️ Пожалуйста, отправьте отредактированный вариант ответа.", cancellationToken: cancellationToken);
                }
                break;
        }
    }

    private InlineKeyboardMarkup GetFollowUpKeyboard()
    {
        return new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔍 Углубить анализ", DeepAnalysisCallback),
                InlineKeyboardButton.WithCallbackData("📬 Проверить почту", CheckEmailCallback),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🧠 Сменить режим", ChangeModeCallback),
                InlineKeyboardButton.WithCallbackData("⚔ Оспорить", ChallengeCallback),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🧹 Очистить контекст", ClearContextCallback),
                InlineKeyboardButton.WithCallbackData("❌ Отмена", CancelCallback),
            }
        });
    }

    private InlineKeyboardMarkup GetDeepAnalysisFollowUpKeyboard()
    {
        return new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔍 Углубить ещё", DeepAnalysisRepeatCallback),
                InlineKeyboardButton.WithCallbackData("⚔ Оспорить вывод", ChallengeAnalysisCallback),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🧠 Сменить режим", ChangeModeCallback),
                InlineKeyboardButton.WithCallbackData("📌 Зафиксировать", FixDecisionCallback),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🧹 Очистить контекст", ClearContextCallback),
                InlineKeyboardButton.WithCallbackData("❌ Отмена", CancelCallback),
            }
        });
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            Telegram.Bot.Exceptions.ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        LoggingService.Logger?.LogError(ErrorMessage);
        return Task.CompletedTask;
    }

    private async Task HandleVoiceMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var userId = message.From!.Id;

        if (UserEmailReplyState.TryGetValue(userId, out var messageId))
        {
            // Handle voice reply for email
            await ProcessVoiceEmailReply(botClient, message, chatId, messageId, cancellationToken);
            UserEmailReplyState.Remove(userId); // Reset state
        }
        else
        {
            // Handle general voice message
            await ProcessGeneralVoiceMessage(botClient, message, chatId, userId.ToString(), cancellationToken);
        }
    }

    private async Task ProcessVoiceEmailReply(ITelegramBotClient botClient, Message message, long chatId, string messageId, CancellationToken cancellationToken)
    {
        var email = _emailCacheService.Get(messageId);
        if (email == null) return;

        var (success, text) = await TranscribeVoiceMessage(botClient, message, cancellationToken);
        if (!success)
        {
            await botClient.SendMessage(chatId: chatId, text: text, cancellationToken: cancellationToken);
            return;
        }

        await botClient.SendMessage(chatId: chatId, text: $"🎙 Распознанный текст:\n«{text}»", cancellationToken: cancellationToken);

        _emailCacheService.AddDraft(messageId, text);
        await botClient.SendMessage(chatId: chatId, text: "Отправляем этот текст?", replyMarkup: GetSendConfirmationKeyboard(messageId), cancellationToken: cancellationToken);
    }

    private async Task ProcessGeneralVoiceMessage(ITelegramBotClient botClient, Message message, long chatId, string userIdString, CancellationToken cancellationToken)
    {
        var (success, text) = await TranscribeVoiceMessage(botClient, message, cancellationToken);
        if (!success)
        {
            await botClient.SendMessage(chatId: chatId, text: text, cancellationToken: cancellationToken);
            return;
        }

        await botClient.SendMessage(chatId: chatId, text: $"🎙 Я понял так:\n«{text}»", cancellationToken: cancellationToken);

        MemoryService.AddMessage(userIdString, text);
        
        await botClient.SendChatAction(chatId: chatId, action: ChatAction.Typing, cancellationToken: cancellationToken);

        var history = MemoryService.GetHistory(userIdString);
        var mode = MemoryService.GetMode(userIdString);
        var response = await GigaChatService.GetDecisionAsync(text, history, mode, cancellationToken);

        MemoryService.SetLastSituation(userIdString, text);
        MemoryService.SetLastGigaChatResponse(userIdString, response);

        await botClient.SendMessage(chatId: chatId, text: response, cancellationToken: cancellationToken);
        await botClient.SendMessage(chatId: chatId, text: "Что дальше?", replyMarkup: GetFollowUpKeyboard(), cancellationToken: cancellationToken);
    }

    private async Task<(bool, string)> TranscribeVoiceMessage(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        var fileId = message.Voice!.FileId;
        var fileInfo = await botClient.GetFile(fileId: fileId, cancellationToken: cancellationToken);
        var filePath = fileInfo.FilePath;

        if (filePath is null)
        {
            return (false, "Не удалось получить информацию о файле.");
        }

        await using var audioStream = new MemoryStream();
        await botClient.DownloadFile(filePath: filePath, destination: audioStream, cancellationToken: cancellationToken);
        audioStream.Position = 0;

        return await _yandexSttService.RecognizeSpeechAsync(audioStream, cancellationToken);
    }
    
    private InlineKeyboardMarkup GetInitialDecisionKeyboard()
    {
        return new(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("🧠 Получить решение", DecisionCallback) },
            new [] { InlineKeyboardButton.WithCallbackData("❌ Ничего", CancelCallback) },
        });
    }

    private InlineKeyboardMarkup GetReplyStyleKeyboard(string messageId)
    {
        return new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Формальный", $"{GenerateReplyCallback}_{messageId}_{ReplyStyle.Formal}"),
                InlineKeyboardButton.WithCallbackData("Деловой", $"{GenerateReplyCallback}_{messageId}_{ReplyStyle.Business}"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Нейтральный", $"{GenerateReplyCallback}_{messageId}_{ReplyStyle.Neutral}"),
                InlineKeyboardButton.WithCallbackData("Дружелюбный", $"{GenerateReplyCallback}_{messageId}_{ReplyStyle.Friendly}"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Краткий", $"{GenerateReplyCallback}_{messageId}_{ReplyStyle.Concise}"),
            },
        });
    }

    private async Task ProcessManualEmailReply(ITelegramBotClient botClient, Message message, long chatId, string messageId, CancellationToken cancellationToken)
    {
        var email = _emailCacheService.Get(messageId);
        if (email == null) return;

        var replyText = message.Text;
        if (string.IsNullOrEmpty(replyText))
        {
            await botClient.SendMessage(chatId: chatId, text: "Пустой ответ не может быть отправлен.", cancellationToken: cancellationToken);
            return;
        }
        _emailCacheService.AddDraft(messageId, replyText);
        await botClient.SendMessage(chatId: chatId, text: $"Черновик:\n\n{replyText}", replyMarkup: GetSendConfirmationKeyboard(messageId), cancellationToken: cancellationToken);
    }

    private InlineKeyboardMarkup GetSendConfirmationKeyboard(string messageId)
    {
        return new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Отправить", $"{SendEmailCallback}_{messageId}"),
                InlineKeyboardButton.WithCallbackData("✏️ Редактировать", $"{EditReplyCallback}_{messageId}"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("❌ Отмена", CancelCallback),
            },
        });
    }
}
