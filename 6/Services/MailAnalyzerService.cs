using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
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
  ""importance"": ""🔴 Критично | 🟠 Важно | 🟢 Информационно"",
  ""type"": ""Запрос | Жалоба | Задача | Финансы | Юридическое | Спам / мусор"",
  ""summary"": ""Краткое резюме письма в 3-5 предложениях"",
  ""action_required"": ""Что конкретно требуется от получателя"",
  ""deadline"": ""Срок выполнения, если указан, в формате YYYY-MM-DD HH:mm"",
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
                var analyzedEmail = JsonSerializer.Deserialize<AnalyzedEmail>(rawResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (analyzedEmail != null)
                {
                    analyzedEmail.OriginalMessage = email;
                    return analyzedEmail;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to deserialize GigaChat response for email analysis: {ex.Message}");
            }
            
            return new AnalyzedEmail
            {
                Importance = "Не удалось определить",
                Type = "Не удалось определить",
                Summary = "Не удалось проанализировать письмо.",
                ActionRequired = "Не удалось определить",
                Deadline = "Не удалось определить",
                Risk = "Не удалось определить",
                OriginalMessage = email
            };
        }
    }
}
