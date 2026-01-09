using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using TelegramGigaChatBot.Models;

namespace TelegramGigaChatBot.Services
{
    public class MailAnalyzerService
    {
        public async Task<AnalyzedEmail> AnalyzeEmailAsync(EmailMessage email, CancellationToken cancellationToken)
        {
            var prompt = $@"
Ты — ИИ-аналитик электронной почты. Проанализируй следующее письмо и верни ТОЛЬКО JSON-объект со следующей структурой:
{{
  ""priority"": ""Одно из: Critical, High, Medium, Low"",
  ""intent"": ""Одно из: Запрос, Жалоба, Предложение, Срочное, Информационное, Другое"",
  ""emotional_tone"": ""Одно из: Нейтральный, Напряженный, Агрессивный, Позитивный, Другое"",
  ""key_data"": {{
    ""questions"": [""список"", ""вопросов"", ""из письма""],
    ""requirements"": [""список"", ""требований""],
    ""deadlines"": [""список"", ""дедлайнов""],
    ""money_mentions"": [""список"", ""упоминаний денег""],
    ""contract_mentions"": [""список"", ""упоминаний договоров""],
    ""problem_mentions"": [""список"", ""упоминаний проблем""]
  }},
  ""summary"": ""Краткое резюме письма в 3-5 предложениях"",
  ""action_required"": ""Что конкретно требуется от получателя"",
  ""risk"": ""Потенциальные риски при игнорировании письма""
}}

ТЕКСТ ПИСЬМА:
От: {email.From}
Тема: {email.Subject}
{email.Body}
";

            var rawResponse = await GigaChatService.GetRawGigaChatResponse(prompt, cancellationToken);
            
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                };
                var analyzedEmail = JsonSerializer.Deserialize<AnalyzedEmail>(rawResponse, options);

                if (analyzedEmail != null)
                {
                    analyzedEmail.OriginalMessage = email;
                    return analyzedEmail;
                }
            }
            catch (JsonException ex)
            {
                LoggingService.Logger?.LogError(ex, "Failed to deserialize GigaChat response. Raw response: {RawResponse}", rawResponse);
            }
            catch (Exception ex)
            {
                LoggingService.Logger?.LogError(ex, "An unexpected error occurred during email analysis.");
            }
            
            return new AnalyzedEmail
            {
                Summary = "Не удалось проанализировать письмо.",
                ActionRequired = "Не удалось определить.",
                OriginalMessage = email,
                Priority = EmailPriority.Medium // Default priority
            };
        }
    }
}
