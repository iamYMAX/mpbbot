using System;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MimeKit;
using TelegramGigaChatBot.Configuration;
using TelegramGigaChatBot.Models;

namespace TelegramGigaChatBot.Services
{
    public class MailReplyService
    {
        private readonly EmailSettings _emailSettings;

        public MailReplyService(EmailSettings emailSettings)
        {
            _emailSettings = emailSettings;
        }

        public async Task<string> GenerateReplyDraftAsync(AnalyzedEmail email, ReplyStyle style, CancellationToken cancellationToken)
        {
            var stylePrompt = ReplyStyleHelper.GetStylePrompt(style);
            var prompt = $@"
Ты — ИИ-ассистент, который помогает писать ответы на электронные письма.
Стиль ответа: {stylePrompt}.

ПИСЬМО, НА КОТОРОЕ НУЖНО ОТВЕТИТЬ:
От: {email.OriginalMessage.From}
Тема: {email.OriginalMessage.Subject}
{email.OriginalMessage.Body}

АНАЛИЗ ПИСЬМА:
{email.Summary}
Требуется: {email.ActionRequired}

Напиши черновик ответа. Ответ должен быть вежливым, по существу и учитывать заданный стиль.
";

            var draft = await GigaChatService.GetRawGigaChatResponse(prompt, cancellationToken);
            // Basic cleanup of the draft
            return draft.Trim().Replace("{\"error\":\"GigaChat service not initialized.\"}", "Не удалось сгенерировать ответ.");
        }

        public async Task<bool> SendReplyAsync(EmailAccount account, string to, string subject, string body, CancellationToken cancellationToken)
        {
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

                LoggingService.Logger?.LogInformation($"Email sent from {account.EmailAddress} to {to}.");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Logger?.LogError(ex, $"Failed to send email from {account.EmailAddress}.");
                return false;
            }
        }
    }
}
