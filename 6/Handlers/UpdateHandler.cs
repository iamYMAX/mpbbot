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

namespace TelegramGigaChatBot.Handlers;

public class UpdateHandler : IUpdateHandler
{
    private static readonly Dictionary<int, AnalyzedEmail> AnalyzedEmailsCache = new();
    private readonly YandexSttService _yandexSttService;
    private readonly MailReaderService _mailReaderService;
    private readonly MailAnalyzerService _mailAnalyzerService;
    private readonly MailReplyService _mailReplyService;

    public UpdateHandler(YandexSttService yandexSttService, MailReaderService mailReaderService, MailAnalyzerService mailAnalyzerService, MailReplyService mailReplyService)
    {
        _yandexSttService = yandexSttService;
        _mailReaderService = mailReaderService;
        _mailAnalyzerService = mailAnalyzerService;
        _mailReplyService = mailReplyService;
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
    private const string CheckEmailCallback = "check_email";
    private const string GenerateReplyCallback = "generate_reply";
    private const string IgnoreEmailCallback = "ignore_email";
    
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
        var userId = message.From.Id.ToString();
        
        Console.WriteLine($"Received a '{messageText}' message in chat {chatId} from user {userId}.");

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

        MemoryService.AddMessage(userId, messageText);

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
            var emailHash = int.Parse(callbackData.Split('_')[1]);
            if (AnalyzedEmailsCache.TryGetValue(emailHash, out var email))
            {
                var mode = MemoryService.GetMode(userId);
                await botClient.SendMessage(chatId: chatId, text: "✍️ Генерирую черновик ответа...", cancellationToken: cancellationToken);
                var draft = await _mailReplyService.GenerateReplyDraftAsync(email, mode, cancellationToken);
                
                // TODO: Add keyboard for sending/editing draft
                await botClient.SendMessage(chatId: chatId, text: $"Черновик:\n\n{draft}", cancellationToken: cancellationToken);
                AnalyzedEmailsCache.Remove(emailHash); // Clean up
            }
            return;
        }

        if (callbackData.StartsWith(IgnoreEmailCallback))
        {
            var emailHash = int.Parse(callbackData.Split('_')[1]);
            if (AnalyzedEmailsCache.ContainsKey(emailHash))
            {
                await botClient.SendMessage(chatId: chatId, text: "🗑 Письмо проигнорировано.", cancellationToken: cancellationToken);
                AnalyzedEmailsCache.Remove(emailHash); // Clean up
            }
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
            
            case CheckEmailCallback:
            {
                await botClient.SendMessage(chatId: chatId, text: "⏳ Проверяю почту...", cancellationToken: cancellationToken);
                var unreadEmails = await _mailReaderService.GetUnreadEmailsAsync(cancellationToken);

                if (!unreadEmails.Any())
                {
                    await botClient.SendMessage(chatId: chatId, text: "👍 Новых писем нет.", cancellationToken: cancellationToken);
                    return;
                }

                await botClient.SendMessage(chatId: chatId, text: $"📥 Найдено новых писем: {unreadEmails.Count}. Начинаю анализ...", cancellationToken: cancellationToken);

                foreach (var email in unreadEmails)
                {
                    var analyzedEmail = await _mailAnalyzerService.AnalyzeEmailAsync(email, cancellationToken);
                    var emailHash = analyzedEmail.GetHashCode();
                    AnalyzedEmailsCache[emailHash] = analyzedEmail;
                    
                    var messageText = $@"
📬 *Новое письмо*
*От:* {analyzedEmail.OriginalMessage.From}
*Тема:* {analyzedEmail.OriginalMessage.Subject}
*Важность:* {analyzedEmail.Importance}

*Кратко:*
{analyzedEmail.Summary}

*Рекомендация:*
{analyzedEmail.ActionRequired}";
                    
                    var emailKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] { InlineKeyboardButton.WithCallbackData("✍️ Сформировать ответ", $"{GenerateReplyCallback}_{email.GetHashCode()}") },
                        new [] { InlineKeyboardButton.WithCallbackData("🗑 Игнорировать", $"{IgnoreEmailCallback}_{email.GetHashCode()}") },
                    });
                    
                    await botClient.SendMessage(chatId: chatId, text: messageText, replyMarkup: emailKeyboard, cancellationToken: cancellationToken);
                }
                break;
            }
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

        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }

    private async Task HandleVoiceMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var userId = message.From!.Id.ToString();

        await botClient.SendChatAction(chatId: chatId, action: ChatAction.Typing, cancellationToken: cancellationToken);
        
        var fileId = message.Voice!.FileId;
        var fileInfo = await botClient.GetFile(fileId: fileId, cancellationToken: cancellationToken);
        var filePath = fileInfo.FilePath;

        if (filePath is null)
        {
            await botClient.SendMessage(chatId: chatId, text: "Не удалось получить информацию о файле.", cancellationToken: cancellationToken);
            return;
        }

        await using var audioStream = new MemoryStream();
        await botClient.DownloadFile(filePath: filePath, destination: audioStream, cancellationToken: cancellationToken);
        audioStream.Position = 0;

        var (success, text) = await _yandexSttService.RecognizeSpeechAsync(audioStream, cancellationToken);

        if (!success)
        {
            await botClient.SendMessage(chatId: chatId, text: text, cancellationToken: cancellationToken);
            return;
        }

        await botClient.SendMessage(chatId: chatId, text: $"🎙 Я понял так:\n«{text}»", cancellationToken: cancellationToken);

        MemoryService.AddMessage(userId, text);
        
        await botClient.SendChatAction(chatId: chatId, action: ChatAction.Typing, cancellationToken: cancellationToken);

        var history = MemoryService.GetHistory(userId);
        var mode = MemoryService.GetMode(userId);
        var response = await GigaChatService.GetDecisionAsync(text, history, mode, cancellationToken);

        MemoryService.SetLastSituation(userId, text);
        MemoryService.SetLastGigaChatResponse(userId, response);

        await botClient.SendMessage(chatId: chatId, text: response, cancellationToken: cancellationToken);
        await botClient.SendMessage(chatId: chatId, text: "Что дальше?", replyMarkup: GetFollowUpKeyboard(), cancellationToken: cancellationToken);
    }
    
    private InlineKeyboardMarkup GetInitialDecisionKeyboard()
    {
        return new(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("🧠 Получить решение", DecisionCallback) },
            new [] { InlineKeyboardButton.WithCallbackData("❌ Ничего", CancelCallback) },
        });
    }
}
