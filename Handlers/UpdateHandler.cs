using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
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
    private readonly YandexSttService _yandexSttService;
    private readonly MailReplyService _mailReplyService;
    private readonly EmailCacheService _emailCacheService;
    private readonly ILogger<UpdateHandler> _logger;

    public UpdateHandler(
        YandexSttService yandexSttService,
        MailReplyService mailReplyService,
        EmailCacheService emailCacheService,
        ILogger<UpdateHandler> logger,
        UserActionStateService userActionStateService)
    {
        _yandexSttService = yandexSttService;
        _mailReplyService = mailReplyService;
        _emailCacheService = emailCacheService;
        _logger = logger;
        _userActionStateService = userActionStateService;
    }
    private readonly UserActionStateService _userActionStateService;
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
    private const string ChooseReplyStyleCallback = "choose_reply_style";
    private const string GenerateReplyForStyleCallback = "generate_reply_style";
    private const string IgnoreEmailCallback = "ignore_email";
    private const string ShowEmailDetailsCallback = "show_email_details";
    private const string StyleFormalCallback = "style_formal";
    private const string StyleBusinessCallback = "style_business";
    private const string StyleNeutralCallback = "style_neutral";
    private const string StyleFriendlyCallback = "style_friendly";
    private const string StyleHardBriefCallback = "style_hard_brief";
    private const string StyleCustomCallback = "style_custom";
    private const string DictateReplyCallback = "dictate_reply";
    private const string InboxCallback = "inbox";
    private const string SendReplyCallback = "send_reply";
    private const string EditReplyCallback = "edit_reply";
    private const string DiscardReplyCallback = "discard_reply";
    
    private readonly ConcurrentDictionary<long, string> _userReplyDrafts = new();

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
        
        var userState = _userActionStateService.GetState(message.From.Id);
        if (userState.CurrentAction == UserAction.EditingEmailReply)
        {
            var messageId = userState.Data;
            _userReplyDrafts[message.From.Id] = messageText;

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new [] { InlineKeyboardButton.WithCallbackData("✅ Отправить", $"{SendReplyCallback}_{messageId}") },
                new [] { InlineKeyboardButton.WithCallbackData("✏️ Редактировать", $"{EditReplyCallback}_{messageId}") },
                new [] { InlineKeyboardButton.WithCallbackData("❌ Отменить", $"{DiscardReplyCallback}_{messageId}") },
            });

            await botClient.SendMessage(chatId: chatId, text: $"Новый черновик:\n\n{messageText}", replyMarkup: keyboard, cancellationToken: cancellationToken);
            _userActionStateService.ClearState(message.From.Id);
            return;
        }

        _logger.LogInformation("Received a '{messageText}' message in chat {chatId} from user {userId}.", messageText, chatId, userId);

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

        _logger.LogInformation("Received callback query '{callbackData}' from user {userId} in chat {chatId}.", callbackData, userId, chatId);

        // Acknowledge the callback query
        await botClient.AnswerCallbackQuery(callbackQueryId: callbackQuery.Id, cancellationToken: cancellationToken);

        // Remove the initial keyboard
        await botClient.EditMessageReplyMarkup(chatId: chatId, messageId: callbackQuery.Message.MessageId, cancellationToken: cancellationToken);

        if (callbackData.StartsWith(InboxCallback))
        {
            var emails = _emailCacheService.GetEmails().ToList();
            if (!emails.Any())
            {
                await botClient.SendMessage(chatId: chatId, text: "👍 Новых писем нет.", cancellationToken: cancellationToken);
                return;
            }

            var sortedEmails = emails.Take(10).GroupBy(e => e.Priority)
                                     .OrderBy(g => g.Key);

            var inlineKeyboardButtons = new List<InlineKeyboardButton[]>();
            foreach (var group in sortedEmails)
            {
                inlineKeyboardButtons.Add(new[] { InlineKeyboardButton.WithCallbackData($"----- {group.Key} -----", "ignore") });
                foreach (var email in group)
                {
                    inlineKeyboardButtons.Add(new[] { InlineKeyboardButton.WithCallbackData(email.OriginalMessage.Subject, $"{ShowEmailDetailsCallback}_{email.OriginalMessage.MessageId}") });
                }
            }

            var inlineKeyboardMarkup = new InlineKeyboardMarkup(inlineKeyboardButtons);
            await botClient.SendMessage(chatId: chatId, text: "📥 *Входящие*", replyMarkup: inlineKeyboardMarkup, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
            return;
        }

        if (callbackData.StartsWith(ChooseReplyStyleCallback))
        {
            var messageId = callbackData.Split('_')[1];
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Формальный", $"{GenerateReplyForStyleCallback}_{messageId}_{ReplyStyle.Formal}") },
                new[] { InlineKeyboardButton.WithCallbackData("Деловой", $"{GenerateReplyForStyleCallback}_{messageId}_{ReplyStyle.Business}") },
                new[] { InlineKeyboardButton.WithCallbackData("Нейтральный", $"{GenerateReplyForStyleCallback}_{messageId}_{ReplyStyle.Neutral}") },
                new[] { InlineKeyboardButton.WithCallbackData("Дружелюбный", $"{GenerateReplyForStyleCallback}_{messageId}_{ReplyStyle.Friendly}") },
                new[] { InlineKeyboardButton.WithCallbackData("Жесткий/Краткий", $"{GenerateReplyForStyleCallback}_{messageId}_{ReplyStyle.HardBrief}") },
                new[] { InlineKeyboardButton.WithCallbackData("Кастомный", $"{GenerateReplyForStyleCallback}_{messageId}_{ReplyStyle.Custom}") },
            });
            await botClient.SendMessage(chatId: chatId, text: "Выберите стиль ответа:", replyMarkup: keyboard, cancellationToken: cancellationToken);
            return;
        }

        if (callbackData.StartsWith(ShowEmailDetailsCallback))
        {
            var messageId = callbackData.Split('_')[1];
            var email = _emailCacheService.GetEmail(messageId);
            if (email != null)
            {
                var response = $@"
*Тема:* {email.OriginalMessage.Subject}
*От:* {email.OriginalMessage.From}

*Анализ:*
- *Тема:* {email.Theme}
- *Намерение:* {email.Intent}
- *Тон:* {email.EmotionalTone}

*Резюме:*
{email.Summary}
                ";

                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new [] { InlineKeyboardButton.WithCallbackData("✍️ Сформировать ответ", $"{ChooseReplyStyleCallback}_{messageId}") },
                    new [] { InlineKeyboardButton.WithCallbackData("🎙 Надиктовать ответ", $"{DictateReplyCallback}_{messageId}") },
                    new [] { InlineKeyboardButton.WithCallbackData("🗑 Игнорировать", $"{IgnoreEmailCallback}_{messageId}") },
                });

                await botClient.SendMessage(chatId: chatId, text: response, replyMarkup: keyboard, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
            }
            return;
        }

        if (callbackData.StartsWith(DictateReplyCallback))
        {
            var messageId = callbackData.Split('_')[1];
            _userActionStateService.SetState(callbackQuery.From.Id, UserAction.DictatingEmailReply, messageId);
            await botClient.SendMessage(chatId: chatId, text: "🎙 Говорите, я записываю...", cancellationToken: cancellationToken);
            return;
        }

        if (callbackData.StartsWith(GenerateReplyForStyleCallback))
        {
            var parts = callbackData.Split('_');
            var messageId = parts[2];
            var style = (ReplyStyle)Enum.Parse(typeof(ReplyStyle), parts[3]);

            var email = _emailCacheService.GetEmail(messageId);
            if (email != null)
            {
                await botClient.SendMessage(chatId: chatId, text: "✍️ Генерирую черновик ответа...", cancellationToken: cancellationToken);
                var draft = await _mailReplyService.GenerateReplyDraftAsync(email, style, cancellationToken);

                _userReplyDrafts[callbackQuery.From.Id] = draft;

                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new [] { InlineKeyboardButton.WithCallbackData("✅ Отправить", $"{SendReplyCallback}_{messageId}") },
                    new [] { InlineKeyboardButton.WithCallbackData("✏️ Редактировать", $"{EditReplyCallback}_{messageId}") },
                    new [] { InlineKeyboardButton.WithCallbackData("❌ Отменить", $"{DiscardReplyCallback}_{messageId}") },
                });
                
                await botClient.SendMessage(chatId: chatId, text: $"Черновик:\n\n{draft}", replyMarkup: keyboard, cancellationToken: cancellationToken);
                _emailCacheService.RemoveEmail(messageId); // Clean up
            }
            return;
        }

        if (callbackData.StartsWith(IgnoreEmailCallback))
        {
            var messageId = callbackData.Split('_')[1];
            var email = _emailCacheService.GetEmail(messageId);
            if (email != null)
            {
                await botClient.SendMessage(chatId: chatId, text: "🗑 Письмо проигнорировано.", cancellationToken: cancellationToken);
                _emailCacheService.RemoveEmail(messageId); // Clean up
            }
            return;
        }
        
        if (callbackData.StartsWith(SendReplyCallback))
        {
            var messageId = callbackData.Split('_')[1];
            var email = _emailCacheService.GetEmail(messageId);
            if (email != null && _userReplyDrafts.TryGetValue(callbackQuery.From.Id, out var draft))
            {
                var success = await _mailReplyService.SendReplyAsync(email.OriginalMessage.Account, email.OriginalMessage.From, $"Re: {email.OriginalMessage.Subject}", draft, cancellationToken);
                if (success)
                {
                    await botClient.SendMessage(chatId: chatId, text: "✅ Ответ успешно отправлен.", cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendMessage(chatId: chatId, text: "❌ Не удалось отправить ответ.", cancellationToken: cancellationToken);
                }
                _userReplyDrafts.TryRemove(callbackQuery.From.Id, out _);
            }
            return;
        }

        if (callbackData.StartsWith(EditReplyCallback))
        {
            var messageId = callbackData.Split('_')[1];
            _userActionStateService.SetState(callbackQuery.From.Id, UserAction.EditingEmailReply, messageId);
            await botClient.SendMessage(chatId: chatId, text: "✏️ Отправьте отредактированный текст ответа.", cancellationToken: cancellationToken);
            return;
        }

        if (callbackData.StartsWith(DiscardReplyCallback))
        {
            _userReplyDrafts.TryRemove(callbackQuery.From.Id, out _);
            await botClient.SendMessage(chatId: chatId, text: "❌ Ответ отменен.", cancellationToken: cancellationToken);
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
        }
    }

    private InlineKeyboardMarkup GetFollowUpKeyboard()
    {
        return new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔍 Углубить анализ", DeepAnalysisCallback),
                InlineKeyboardButton.WithCallbackData("📥 Входящие", InboxCallback),
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
        var errorMessage = exception switch
        {
            Telegram.Bot.Exceptions.ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogError(errorMessage);
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

        var userState = _userActionStateService.GetState(message.From.Id);
        if (userState.CurrentAction == UserAction.DictatingEmailReply)
        {
            _userReplyDrafts[message.From.Id] = text;
            var messageId = userState.Data;

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new [] { InlineKeyboardButton.WithCallbackData("✅ Отправить", $"{SendReplyCallback}_{messageId}") },
                new [] { InlineKeyboardButton.WithCallbackData("✏️ Редактировать", $"{EditReplyCallback}_{messageId}") },
                new [] { InlineKeyboardButton.WithCallbackData("❌ Отменить", $"{DiscardReplyCallback}_{messageId}") },
            });

            await botClient.SendMessage(chatId: chatId, text: $"🎙 Ваш ответ:\n«{text}»", replyMarkup: keyboard, cancellationToken: cancellationToken);
            _userActionStateService.ClearState(message.From.Id);
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
