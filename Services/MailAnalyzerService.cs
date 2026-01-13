using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TelegramGigaChatBot.Models;

namespace TelegramGigaChatBot.Services
{
    public class MailAnalyzerService
    {
        private readonly GigaChatService _gigaChatService;
        private readonly ILogger<MailAnalyzerService> _logger;

        public MailAnalyzerService(GigaChatService gigaChatService, ILogger<MailAnalyzerService> logger)
        {
            _gigaChatService = gigaChatService;
            _logger = logger;
        }

        public async Task<EmailAnalysisResult> AnalyzeEmailAsync(EmailMessage email, CancellationToken cancellationToken)
        {
            var prompt = $@"
Ты — ИИ-аналитик электронной почты. Проанализируй следующее письмо и верни ТОЛЬКО JSON-объект со следующей структурой:
{{
  ""sender_type"": ""Определи роль отправителя (клиент, коллега, руководитель, подрядчик, внешний запрос, система/автомат)"",
  ""theme"": ""Определи основную тему письма (Продажи, Техподдержка, Проект, HR, Финансы, Личное)"",
  ""intent"": ""Определи намерение (Запрос информации, Постановка задачи, Жалоба, Предложение, Уведомление)"",
  ""emotional_tone"": ""Определи эмоциональный тон (Нейтральный, Позитивный, Негативный, Напряженный, Агрессивный)"",
  ""has_questions"": true/false,
  ""has_deadlines"": true/false,
  ""has_money_mention"": true/false,
  ""has_legal_mention"": true/false,
  ""summary"": ""Сформулируй краткое резюме письма в 2-3 предложениях"",
  ""priority"": ""Присвой приоритет (Critical, High, Medium, Low) на основе содержания, тона и наличия дедлайнов/финансов/юридических вопросов""
}}

ТЕКСТ ПИСЬМА:
От: {email.From}
Тема: {email.Subject}
{email.Body}
";

            var rawResponse = await _gigaChatService.GetRawGigaChatResponse(prompt, cancellationToken);

            try
            {
                var analysisResult = JsonSerializer.Deserialize<EmailAnalysisResult>(rawResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (analysisResult != null)
                {
                    analysisResult.OriginalMessage = email;
                    return analysisResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize GigaChat response for email analysis.");
            }
            
            return new EmailAnalysisResult
            {
                Summary = "Не удалось проанализировать письмо.",
                Priority = "Medium",
                OriginalMessage = email
            };
        }
    }
}
