using System;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using MimeKit;
using TelegramGigaChatBot.Configuration;
using TelegramGigaChatBot.Models;

namespace TelegramGigaChatBot.Services
{
    public class MailReplyService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<MailReplyService> _logger;

        public MailReplyService(AppSettings settings, ILogger<MailReplyService> logger)
        {
            _emailSettings = settings.EmailSettings;
            _logger = logger;
        }

        public async Task<string> GenerateReplyDraftAsync(EmailAnalysisResult email, ReplyStyle style, CancellationToken cancellationToken, string customStyle = "")
        {
            var stylePrompt = ReplyStyleHelper.GetStylePrompt(style, customStyle);
            var prompt = $@"
Ты — ИИ-ассистент, который помогает писать ответы на электронные письма.
Стиль ответа: {stylePrompt}.

ПИСЬМО, НА КОТОРОЕ НУЖНО ОТВЕТИТЬ:
От: {email.OriginalMessage.From}
Тема: {email.OriginalMessage.Subject}
{email.OriginalMessage.Body}

АНАЛИЗ ПИСЬМА:
{email.Summary}

Напиши черновик ответа. Ответ должен быть вежливым, по существу и учитывать заданный стиль.
";

            var draft = await GigaChatService.GetRawGigaChatResponse(prompt, cancellationToken);
            // Basic cleanup of the draft
            return draft.Trim().Replace("{\"error\":\"GigaChat service not initialized.\"}", "Не удалось сгенерировать ответ.");
        }

        public async Task<bool> SendReplyAsync(EmailAccount account, string to, string subject, string body, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to send email from {EmailAddress} to {To}.", account.EmailAddress, to);
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(account.EmailAddress, account.EmailAddress));
                message.To.Add(new MailboxAddress(to, to));
                message.Subject = subject;
                message.Body = new TextPart("plain") { Text = body };

                using var client = new SmtpClient();
                await client.ConnectAsync(account.SmtpHost, account.SmtpPort, account.SmtpUseSsl, cancellationToken);
                
                var password = SecurityService.Decrypt(account.Password, _emailSettings.EncryptionKey);
                await client.AuthenticateAsync(account.Login, password, cancellationToken);
                
                await client.SendAsync(message, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);
                
                _logger.LogInformation("Successfully sent email from {EmailAddress} to {To}.", account.EmailAddress, to);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email from {EmailAddress}.", account.EmailAddress);
                return false;
            }
        }
    }
}
